using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using Danmokou.Core.DInput;
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
    void RegularUpdateParallel() { }

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
    void FirstFrame() { }

    /// <summary>
    /// Updater priority. Lower priority = goes first. Note that order is not guaranteed during
    /// the parallel update section.
    /// </summary>
    int UpdatePriority => UpdatePriorities.DEFAULT;

    /// <summary>
    /// The maximum engine state that the object can act upon. Set to RUN for most objects.
    /// </summary>
    EngineState UpdateDuring => EngineState.RUN;
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
    private static bool UsePauseHandling => EngineStateManager.State > EngineState.RUN;
    private float untilNextRegularFrame = 0f;
    private float untilNextPauseFrame = 0f;
    private float UntilNextFrame {
        get => UsePauseHandling ? untilNextPauseFrame : untilNextRegularFrame;
        set {
            if (UsePauseHandling)
                untilNextPauseFrame = value;
            else
                untilNextRegularFrame = value;
        }
    }
    public const int ENGINEFPS = 120;
    public const float ENGINEFPS_F = ENGINEFPS;
    public const float FRAME_TIME = 1f / ENGINEFPS_F;
    public const float FRAME_YIELD = FRAME_TIME * 0.1f;
    public static DisturbedProduct<float> Slowdown { get; } = new DisturbedProduct<float>(1f);
    private static DisturbedProduct<float> UnityTimeRate { get; } = new DisturbedProduct<float>(1f);
    /// <summary>
    /// Replacement for Time.dT. Generally fixed to 1/(SCREEN REFRESH RATE),
    /// except during slowdown. Is 0 when the game is paused.
    /// </summary>
    //Note: we dynamically query Slowdown.Value in dT because it might update during the frame,
    // and such updates should be handled ASAP to be responsive. (I do not believe it affects correctness/replays.)
    public static float dT => UsePauseHandling ? 0 : (ASSUME_SCREEN_FRAME_TIME * Slowdown.Value);
    private static float EngineStepTime =>
        UsePauseHandling ? ASSUME_SCREEN_FRAME_TIME : (ASSUME_SCREEN_FRAME_TIME * Slowdown.Value);
    public static int FrameNumber { get; private set; }

    public static void ResetFrameNumber() {
        Logs.Log("Resetting frame counter");
        FrameNumber = 0;
    }

    public static bool FirstUpdateForScreen { get; private set; }
    /// <summary>
    /// Note: when applying slowdown effects, this may be set to true multiple times within a unity frame,
    /// or it may be skipped for one unity frame when the slowdown changes.
    /// As such, limit usage to cosmetics.
    /// </summary>
    public static bool LastUpdateForScreen { get; private set; }
    private static readonly DMCompactingArray<IRegularUpdater> updaters = new DMCompactingArray<IRegularUpdater>();
    private static readonly Queue<(Action, EngineState)> eofInvokes = new Queue<(Action, EngineState)>();
    private static readonly List<(Action, EngineState)> persistentEofInvokes = new List<(Action, EngineState)>();
    private static readonly List<(Action, EngineState)> persistentSofInvokes = new List<(Action, EngineState)>();

    private void Awake() {
        TransitionHelpers.DefaultDeltaTimeProvider = () => FRAME_TIME;
        GenericOps.RegisterType<Vector2>(Vector2.LerpUnclamped, (x, y) => x * y, 
            (Vector2.zero, (x, y) => x + y), (Vector2.one, (x, y) => x * y));
        GenericOps.RegisterType<Vector3>(Vector3.LerpUnclamped, (x, y) => x * y, 
            (Vector3.zero, (x, y) => x + y), (Vector3.one, (a, b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z)));
        GenericOps.RegisterType<Vector4>(Vector4.LerpUnclamped, (x, y) => x * y, 
            (Vector4.zero, (x, y) => x + y), (Vector4.one, (a, b) => new Vector4(a.x * b.x, a.y * b.y, a.z * b.z, a.w * b.w)));
        GenericOps.RegisterType<Color>(Color.LerpUnclamped, (x, y) => x * y, 
            (Color.clear, (x, y) => x + y), (Color.white, (x, y) => x * y));

        SceneIntermediary.Attach();
        SceneIntermediary.SceneLoaded.Subscribe(_ => untilNextRegularFrame = 0f);

        UnityTimeRate.AddDisturbance(Slowdown);
        UnityTimeRate.AddDisturbance(new MappedObservable<EngineState, float>(EngineStateManager.EvState, s => s.Timescale()));
        UnityTimeRate.Subscribe(s => Time.timeScale = s);

        //WARNING ON TIMESCALE: You must also modify FDT. See https://docs.unity3d.com/ScriptReference/Time-timeScale.html
        //This said, I don't think FixedUpdate is used anymore in this code.

        //Time.timeScale = 0.5f;
        //Time.fixedDeltaTime *= 0.5f;

        Application.targetFrameRate = -1;

        StartCoroutine(NoVsyncHandler());
    }

    private static void SetForcedFPS(int fps) {
        Logs.Log($"Assuming the screen runs at {fps} fps");
        ASSUME_SCREEN_FRAME_TIME = 1f / fps;
        //Better to use precise thread waiter. See https://blogs.unity3d.com/2019/06/03/precise-framerates-in-unity/
        // This said, mobile requires targetFrameRate.
    #if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = fps;
        //Required to make the set work on startup
        QueueEOFInvoke(() => Application.targetFrameRate = fps);
    #endif
    }

    private static bool ThreadWaiter = false;
    private static float lastFrameTime = 0;

    public static void SetVSync(int ct) {
#if UNITY_ANDROID || UNITY_IOS
        //Mobile uses targetFrameRate only
        Logs.Log("Disabling Vsync for mobile");
        ThreadWaiter = false;
        QualitySettings.vSyncCount = 0;
#else
        Logs.Log($"Setting VSync to {ct}");
        ThreadWaiter = ct == 0;
        QualitySettings.vSyncCount = ct;
#endif
        lastFrameTime = Time.realtimeSinceStartup;
        SetForcedFPS(Screen.currentResolution.refreshRate);
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
            DeletionMarker<IRegularUpdater> updater = updaters.Data[ii];
            if (!updater.MarkedForDeletion) updater.Value.RegularUpdateParallel();
        });
        Profiler.EndSample();
        RNG.RNG_ALLOWED = true;
    }

    private void Update() {
        try {
            FirstUpdateForScreen = true;
            for (; UntilNextFrame + EngineStepTime > FRAME_BOUNDARY;) {
                //If the unity frame is skipped, then don't destroy trigger-based controls.
                //If this toggle is moved out of the loop, then it is possible for trigger-based controls
                // to be ignored if the unity framerate is faster than the game update rate
                // (eg. 240hz, or 60hz + slowdown 0.25).
                if (FirstUpdateForScreen) InputManager.OncePerUnityFrameToggleControls();
                UntilNextFrame -= FRAME_TIME;
                LastUpdateForScreen = UntilNextFrame + EngineStepTime <= FRAME_BOUNDARY;
                if (EngineStateManager.PendingUpdate) continue;
                StartOfFrameInvokes(EngineStateManager.State);
                //Parallelize updates if there are many. Note that this allocates ~2kb
                if (updaters.Count < PARALLELCUTOFF || UsePauseHandling) {
                    for (int ii = 0; ii < updaters.Count; ++ii) {
                        DeletionMarker<IRegularUpdater> updater = updaters.Data[ii];
                        if (!updater.MarkedForDeletion && updater.Value.UpdateDuring >= EngineStateManager.State)
                            updater.Value.RegularUpdateParallel();
                    }
                } else ParallelUpdateStep();
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    DeletionMarker<IRegularUpdater> updater = updaters.Data[ii];
                    if (!updater.MarkedForDeletion && updater.Value.UpdateDuring >= EngineStateManager.State) 
                        updater.Value.RegularUpdate();
                }
                updaters.Compact();
                //Note: The updaters array is only modified by this command. 
                FlushUpdaterAdds();
                EndOfFrameInvokes(EngineStateManager.State);
                if (EngineStateManager.State == EngineState.RUN)
                    FrameNumber++;
                FirstUpdateForScreen = false;
            }
            UntilNextFrame += EngineStepTime;
            if (Mathf.Abs(UntilNextFrame) < FRAME_YIELD) UntilNextFrame = 0f;
            
            EngineStateManager.UpdateEngineState();
        } catch (Exception e) {
            Logs.UnityError("Error thrown in the ETime update loop.");
            Logs.LogException(e);
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
    
    private const int NTHREADS = 8;
    private const int PARALLELCUTOFF = 256;

    private const float FRAME_BOUNDARY = FRAME_TIME - FRAME_YIELD;

    private static void StartOfFrameInvokes(EngineState state) {
        for (int ii = 0; ii < persistentSofInvokes.Count; ++ii) {
            var (act, st) = persistentSofInvokes[ii];
            if (st >= state)
                act();
        }
    }

    private static void EndOfFrameInvokes(EngineState state) {
        for (int ii = 0; ii < persistentEofInvokes.Count; ++ii) {
            var (act, st) = persistentEofInvokes[ii];
            if (st >= state)
                act();
        }
        var neofInv = eofInvokes.Count;
        for (int ii = 0; ii < neofInv; ++ii) {
            var (act, st) = eofInvokes.Dequeue();
            if (st >= state)
                act();
            else
                eofInvokes.Enqueue((act, st));
        }
    }

    public static void RegisterPersistentSOFInvoke(Action act, EngineState state = EngineState.RUN) => 
        persistentSofInvokes.Add((act, state));
    public static void RegisterPersistentEOFInvoke(Action act, EngineState state = EngineState.RUN) => 
        persistentEofInvokes.Add((act, state));
    public static void QueueEOFInvoke(Action act, EngineState state = EngineState.RUN) => eofInvokes.Enqueue((act, state));

    private static readonly Queue<DeletionMarker<IRegularUpdater>> updaterAddQueue =
        new Queue<DeletionMarker<IRegularUpdater>>();

    private static void FlushUpdaterAdds() {
        while (updaterAddQueue.Count > 0) {
            var dm = updaterAddQueue.Dequeue();
            if (!dm.MarkedForDeletion) {
                dm.Value.FirstFrame();
                updaters.AddPriority(dm);
            }
        }
    }

    public static DeletionMarker<IRegularUpdater> RegisterRegularUpdater(IRegularUpdater iru) {
        var dm = new DeletionMarker<IRegularUpdater>(iru, iru.UpdatePriority);
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
        public EngineState UpdateDuring => EngineState.RUN;

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

        private Timer(string name) {
            this.name = name;
            token = RegisterRegularUpdater(this);
        }

        private readonly DeletionMarker<IRegularUpdater> token;

        public static Timer GetTimer(string name) {
            if (!timerMap.TryGetValue(name, out Timer t)) {
                t = timerMap[name] = new Timer(name);
            }
            return t;
        }

        public static Timer PhaseTimer => GetTimer("phaset");

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
        Logs.CloseLog();
    }
}
}
