using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Danmokou.DMath;
using Danmokou.Scenes;
using JetBrains.Annotations;
using UnityEngine.Profiling;


namespace Danmokou.Core {
/// <summary>
/// To get callbacks for engine frame updates, implement this interface and register yourself to TimeManagement.
/// The RegularUpdater class does this automatically.
/// </summary>
public interface IRegularUpdater {
    /// <summary>
    /// Perform a regular update. This function will be called every ONE engine frame,
    /// with a deltaTime of TimeManagement.FRAME_TIME. This function may be called in parallel,
    /// and may not have access to the Unity thread context.
    /// This function is called right before RegularUpdate. Parallel is called on ALL updaters
    /// before any RegUpd are called.
    /// </summary>
    void RegularUpdateParallel();

    /// <summary>
    /// Perform a regular update. This function will be called every ONE engine frame,
    /// with a deltaTime of TimeManagement.FRAME_TIME. This function is called sequentially,
    /// and has access to the Unity thread context.
    /// </summary>
    void RegularUpdate();

    /// <summary>
    /// This function is called at the *end* of the engine frame that an object was registered
    /// or re-registered for regular updates.
    /// In the standard case, this means that it is called on the same frame as Awake, but after all Awake calls.
    /// Works with pooled objects (will be called after ResetV). Similar to Unity's Start.
    /// </summary>
    void FirstFrame();

    /// <summary>
    /// Updater priority. Lower priority = goes first. Note that order is not guaranteed during
    /// the parallel update section.
    /// </summary>
    int UpdatePriority { get; }

    /// <summary>
    /// True iff the object should receive updates while the game is paused. Primarily for utilities.
    /// </summary>
    bool UpdateDuringPause { get; }
}

public static class UpdatePriorities {
    public const int SOF = -100;
    public const int SYSTEM = -40;
    public const int PLAYER = -30;
    public const int PLAYER2 = -29;
    public const int BM = -20;
    public const int BULLET = -15;
    public const int BEH = -10;
    public const int DEFAULT = 0;
    public const int SLOW = 20;
    public const int EOF = 100;
}

public class ETime : MonoBehaviour {
    public static float ASSUME_SCREEN_FRAME_TIME { get; private set; } = 1 / 60f;
    private float untilNextRegularFrame = 0f;
    public const int ENGINEFPS = 120;
    public const float ENGINEFPS_F = ENGINEFPS;
    public const float FRAME_TIME = 1f / ENGINEFPS_F;
    public const float FRAME_YIELD = FRAME_TIME * 0.1f;
    public static MultiMultiplier Slowdown { get; } = new MultiMultiplier(1f, v => Time.timeScale = v);
    /// <summary>
    /// Replacement for Time.dT. Generally fixed to 1/(SCREEN REFRESH RATE),
    /// except during slowdown.
    /// </summary>
    public static float dT => noSlowDT * Slowdown.Value;
    private static float noSlowDT;
    public static int FrameNumber { get; private set; }

    public static void ResetFrameNumber() {
        Log.Unity("Resetting frame counter");
        FrameNumber = 0;
    }

    public static bool FirstUpdateForScreen { get; private set; }
    /// <summary>
    /// Note: when applying slowdown effects, this may be set to true multiple times within a unity frame.
    /// As such, limit usage to cosmetics.
    /// </summary>
    public static bool LastUpdateForScreen { get; private set; }
    private static readonly DMCompactingArray<IRegularUpdater> updaters = new DMCompactingArray<IRegularUpdater>();
    private static readonly Queue<Action> eofInvokes = new Queue<Action>();
    private static readonly Queue<(int remFrames, Action whenZero)> delayedeofInvokes = new Queue<(int, Action)>();
    private static readonly List<Action> persistentEofInvokes = new List<Action>();
    private static readonly List<Action> persistentSofInvokes = new List<Action>();

    private void Awake() {
        SceneIntermediary.Attach();
        SceneIntermediary.RegisterSceneLoad(() => untilNextRegularFrame = 0f);

        //WARNING ON TIMESCALE: You must also modify FDT. See https://docs.unity3d.com/ScriptReference/Time-timeScale.html
        //This said, I don't think FixedUpdate is used anymore in this code.

        //Time.timeScale = 0.5f;
        //Time.fixedDeltaTime *= 0.5f;

        Application.targetFrameRate = -1;

        StartCoroutine(NoVsyncHandler());
    }

    public static void SetForcedFPS(int fps) {
        Log.Unity($"Assuming the screen runs at {fps} fps");
        ASSUME_SCREEN_FRAME_TIME = (fps > 0) ? 1f / fps : 1f / 60f;
        //Better to use precise thread waiter. See https://blogs.unity3d.com/2019/06/03/precise-framerates-in-unity/
        //Application.targetFrameRate = fps;
    }

    private static bool ThreadWaiter = false;
    private static float lastFrameTime = 0;

    public static void SetVSync(int ct) {
        ThreadWaiter = ct == 0;
        lastFrameTime = Time.realtimeSinceStartup;
        QualitySettings.vSyncCount = ct;
    }

    private static void ParallelUpdateStep() {
        //Parallel.For is faster, but allocates more garbage (2.2 vs 1.3 kb per Unity frame). 
        //This is probably because it uses more threads (on my machine).

        /*Profiler.BeginSample("Parallel update- threadpool");
        countdown.Reset(NTHREADS);
        for (int offi = 0; offi < NTHREADS; ++offi) {
            ThreadPool.QueueUserWorkItem(offset => {
                for (int ii = (int)offset; ii < updaters.Count; ii += NTHREADS) {
                    DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                    if (!updater.markedForDeletion) updater.obj.RegularUpdateParallel();
                }
                countdown.Signal();
            }, offi);
        }
        countdown.Wait();*/

        RNG.RNG_ALLOWED = false;
        Profiler.BeginSample("Parallel update");
        Parallel.For(0, updaters.Count, ii => {
            DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
            if (!updater.markedForDeletion) updater.obj.RegularUpdateParallel();
        });
        Profiler.EndSample();
        RNG.RNG_ALLOWED = true;
    }

    private void Update() {
        try {
            FirstUpdateForScreen = true;
            //Updates still go out on loading. Player movement is disabled but other things need to run
            noSlowDT = ASSUME_SCREEN_FRAME_TIME;
            if (EngineStateManager.IsLoadingOrPaused) {
                InputManager.OncePerFrameToggleControls();
                //Send out limited updates ignoring slowdown
                for (; noSlowDT > FRAME_BOUNDARY;) {
                    noSlowDT -= FRAME_TIME;
                    LastUpdateForScreen = noSlowDT <= FRAME_BOUNDARY;
                    EngineStateManager.CheckForStateUpdates();
                    if (EngineStateManager.PendingChange) continue;

                    for (int ii = 0; ii < updaters.Count; ++ii) {
                        DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                        if (!updater.markedForDeletion && updater.obj.UpdateDuringPause) updater.obj.RegularUpdate();
                    }
                    updaters.Compact();
                    FlushUpdaterAdds();
                    FirstUpdateForScreen = false;
                }
                noSlowDT = 0;
            } else {
                for (; untilNextRegularFrame + dT > FRAME_BOUNDARY;) {
                    //If the unity frame is skipped, then don't destroy trigger-based controls.
                    //If this toggle is moved out of the loop, then it is possible for trigger-based controls
                    // to be ignored if the unity framerate is faster than the game update rate
                    // (eg. 240hz, or 60hz + slowdown 0.25).
                    if (FirstUpdateForScreen) InputManager.OncePerFrameToggleControls();
                    untilNextRegularFrame -= FRAME_TIME;
                    LastUpdateForScreen = untilNextRegularFrame + dT <= FRAME_BOUNDARY;
                    EngineStateManager.CheckForStateUpdates();
                    if (EngineStateManager.PendingChange) continue;
                    StartOfFrameInvokes();
                    //Parallelize updates if there are many. Note that this allocates ~2kb
                    if (updaters.Count < PARALLELCUTOFF) {
                        for (int ii = 0; ii < updaters.Count; ++ii) {
                            DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                            if (!updater.markedForDeletion) updater.obj.RegularUpdateParallel();
                        }
                    } else ParallelUpdateStep();
                    for (int ii = 0; ii < updaters.Count; ++ii) {
                        DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                        if (!updater.markedForDeletion) updater.obj.RegularUpdate();
                    }
                    updaters.Compact();
                    //Note: The updaters array is only modified by this command. 
                    FlushUpdaterAdds();
                    EndOfFrameInvokes();
                    FrameNumber++;
                    FirstUpdateForScreen = false;
                }
                untilNextRegularFrame += dT;
                if (Mathf.Abs(untilNextRegularFrame) < FRAME_YIELD) untilNextRegularFrame = 0f;
            }
            EngineStateManager.UpdateGameState();
        } catch (Exception e) {
            Log.UnityError("Error thrown in the ETime update loop.");
            Log.UnityException(e);
            throw;
        }
    }

    private static IEnumerator NoVsyncHandler() {
        while (true) {
            yield return new WaitForEndOfFrame();
            lastFrameTime += ASSUME_SCREEN_FRAME_TIME;
            //In case of lag spikes (eg. on scene load), don't try to recuperate the frames
            lastFrameTime = Math.Max(lastFrameTime, Time.realtimeSinceStartup);
            if (ThreadWaiter) {
                //Profiler.BeginSample("No-Vsync Synchronizer");
                var sleepTime = lastFrameTime - Time.realtimeSinceStartup - 0.01f;
                if (sleepTime > 0) Thread.Sleep((int) (sleepTime * 1000));
                while (Time.realtimeSinceStartup < lastFrameTime) { }
                //Profiler.EndSample();
            }
        }
        // ReSharper disable once IteratorNeverReturns
    }


    private static readonly CountdownEvent countdown = new CountdownEvent(1);
    private const int NTHREADS = 8;
    private const int PARALLELCUTOFF = 256;

    private const float FRAME_BOUNDARY = FRAME_TIME - FRAME_YIELD;

    private static void StartOfFrameInvokes() {
        for (int ii = 0; ii < persistentSofInvokes.Count; ++ii) persistentSofInvokes[ii]();
    }

    private static void EndOfFrameInvokes() {
        for (int ii = 0; ii < persistentEofInvokes.Count; ++ii) persistentEofInvokes[ii]();
        int ndelinv = delayedeofInvokes.Count;
        for (int ii = 0; ii < ndelinv; ++ii) {
            var (remFrames, onZero) = delayedeofInvokes.Dequeue();
            if (remFrames == 0) eofInvokes.Enqueue(onZero);
            else delayedeofInvokes.Enqueue((remFrames - 1, onZero));
        }
        while (eofInvokes.Count > 0) eofInvokes.Dequeue()();
    }

    public static void RegisterPersistentSOFInvoke(Action act) => persistentSofInvokes.Add(act);
    public static void RegisterPersistentEOFInvoke(Action act) => persistentEofInvokes.Add(act);
    public static void QueueEOFInvoke(Action act) => eofInvokes.Enqueue(act);

    public static void QueueDelayedEOFInvoke(int frame_delay, Action act) =>
        delayedeofInvokes.Enqueue((frame_delay, act));

    private static readonly Queue<DeletionMarker<IRegularUpdater>> updaterAddQueue =
        new Queue<DeletionMarker<IRegularUpdater>>();

    private static void FlushUpdaterAdds() {
        while (updaterAddQueue.Count > 0) {
            var dm = updaterAddQueue.Dequeue();
            dm.obj.FirstFrame();
            updaters.AddPriority(dm);
        }
    }

    public static DeletionMarker<IRegularUpdater> RegisterRegularUpdater(IRegularUpdater iru) {
        var dm = DeletionMarker<IRegularUpdater>.Get(iru, iru.UpdatePriority);
        updaterAddQueue.Enqueue(dm);
        return dm;
    }


    /// <summary>
    /// Script and FF-viewable stopwatches that move with game time.
    /// </summary>
    public class Timer : IRegularUpdater {
        public string name;
        /// <summary>
        /// Frame counter, with multiplier built-in.
        /// </summary>
        public float Frames = 0f;
        public float Seconds => Frames * FRAME_TIME;
        /// <summary>
        /// Speed multiplier.
        /// </summary>
        private float multiplier = 1f;
        /// <summary>
        /// True iff the timer is currently accumulating.
        /// </summary>
        private bool enabled = false;
        private readonly bool phaseLocal;

        private void Start(float mult) {
            multiplier = mult;
            enabled = true;
        }

        public void Restart(float mult = 1f) {
            Frames = 0;
            multiplier = mult;
            enabled = true;
        }

        private void Stop() {
            enabled = false;
        }

        public int UpdatePriority => UpdatePriorities.SYSTEM;
        public bool UpdateDuringPause => false;

        public void RegularUpdateParallel() { }

        public void RegularUpdate() {
            if (enabled) Frames += multiplier;
        }

        public void FirstFrame() { }

        private static readonly Dictionary<string, Timer> timerMap = new Dictionary<string, Timer>();

        //SM-viewable functions
        public static void Start(Timer timer, float multiplier = 1f) {
            timer.Start(multiplier);
        }

        public static void Restart(Timer timer, float multiplier = 1f) {
            timer.Restart(multiplier);
        }

        public static void Stop(Timer timer) {
            timer.Stop();
        }

        private Timer(string name, bool phaseLocal) {
            this.name = name;
            this.phaseLocal = phaseLocal;
            token = RegisterRegularUpdater(this);
        }

        private readonly DeletionMarker<IRegularUpdater> token;

        public static Timer GetTimer(string name, bool phaseLocal=true) {
            if (!timerMap.TryGetValue(name, out Timer t)) {
                t = timerMap[name] = new Timer(name, phaseLocal);
            }
            return t;
        }

        public static Timer PhaseTimer => GetTimer("phaset");

        public static void ResetPhase() {
            foreach (var v in timerMap.Values.ToArray()) {
                if (v.phaseLocal) {
                    v.Restart();
                    v.Stop();
                }
            }
        }

        public static void DestroyAll() {
            foreach (var v in timerMap.Values.ToArray()) {
                v.token.MarkForDeletion();
            }
            timerMap.Clear();
        }

        public Expression exFrames => Expression.PropertyOrField(Expression.Constant(this), "Frames");
        public Expression exSeconds => Expression.PropertyOrField(Expression.Constant(this), "Seconds");
    }

    public void OnDestroy() {
        Log.CloseLog();
    }
}
}
