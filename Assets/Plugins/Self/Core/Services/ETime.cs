using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine.Profiling;

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
    /// Will be called before the scene or application is closed (ie. before OnDisable).
    /// </summary>
    void PreSceneClose();
    /// <summary>
    /// Updater priority. Lower priority = goes first. Note that order is not guaranteed during
    /// the parallel update section.
    /// </summary>
    int UpdatePriority { get; }
    
    /// <summary>
    /// Whether or not this object should receive partial updates in cases where
    /// a Unity frame is less time than an engine frame (ie. during slowdown).
    /// </summary>
    bool ReceivePartialUpdates { get; }
    
    /// <summary>
    /// Only called if ReceivePartialUpdates is set. Perform a partial update with some time that is
    /// less than FRAME_TIME.
    /// WARNING: Consider the case where we are running at a 2x slowdown. Normal updaters will update like:
    /// X FULL X FULL X FULL
    /// whereas partial updaters will update like:
    /// 1/2 FULL 1/2 FULL 1/2 FULL
    /// You need to implement your own "fatiguing" system so that the full updates are discounted accordingly.
    /// </summary>
    /// <param name="dT"></param>
    void PartialUpdate(float dT);
    
    /// <summary>
    /// True iff the object should receive updates while the game is paused. Primarily for utilities.
    /// </summary>
    bool UpdateDuringPause { get; }
}
/*
/// <summary>
/// To get callbacks for Unity updates, implement this interface and register yourself to TimeManagement.
/// These updaters are called every Unity frame, so the engine time may be variable.
/// Basically the same as using Update(), but this has priority handling.
/// This is resolved before RegularUpdate on the same frame.
/// </summary>
public interface IVariableUpdater {
    void VariableUpdate(float dT);
    /// <summary>
    /// Updater priority. Lower priority = goes first.
    /// </summary>
    int UpdatePriority { get; }

}*/
public static class UpdatePriorities {
    public const int SOF = -100;
    public const int SYSTEM = -40;
    public const int PLAYER = -30;
    public const int PLAYER2 = -29;
    public const int BM = -20;
    public const int BEH = -10;
    public const int DEFAULT = 0;
    public const int SLOW = 20;
    public const int EOF = 100;
}

public class ETime : MonoBehaviour {
    public static float ASSUME_SCREEN_FRAME_TIME { get; private set; } = 1 / 60f;
    private float untilNextRegularFrame = 0f;
    public const float ENGINEFPS = 120f;
    public const float FRAME_TIME = 1f / ENGINEFPS;
    public const float FRAME_YIELD = FRAME_TIME * 0.1f;
    public static float Slowdown { get; private set; } = 1f;
    /// <summary>
    /// Replacement for Time.dT. Generally fixed to 1/60.
    /// </summary>
    public static float dT => noSlowDT * Slowdown;
    private static float noSlowDT;
    public static int FrameNumber { get; private set; }
    public static float SCREENFPS { get; private set; }
    public static bool LastUpdateForScreen { get; private set; }
    public static float ENGINEPERSCREENFPS => ENGINEFPS / SCREENFPS;
    private static readonly DMCompactingArray<IRegularUpdater> updaters = new DMCompactingArray<IRegularUpdater>();
    private static readonly Queue<Action> eofInvokes = new Queue<Action>();
    private static readonly Queue<(int remFrames, Action whenZero)> delayedeofInvokes = new Queue<(int, Action)>();
    private static readonly List<Action> persistentEofInvokes = new List<Action>();
    private static readonly List<Action> persistentSofInvokes = new List<Action>();
    
    private void OnApplicationQuit() {
        PrepareSceneClose();
    }

    private void PrepareSceneClose() {
        updaters.ForEachIfNotCancelled(x => x.PreSceneClose());
    }
    
    private void Awake() {
        SCREENFPS = 60;
        SceneIntermediary.Attach();
        SceneIntermediary.RegisterPreSceneUnload(PrepareSceneClose);
        SceneIntermediary.RegisterSceneLoad(() => untilNextRegularFrame = 0f);
        //WARNING ON TIMESCALE: You must also modify FDT. See https://docs.unity3d.com/ScriptReference/Time-timeScale.html
        //This said, I don't think FixedUpdate is used anymore in this code.

        //Time.timeScale = 0.5f;
        //Time.fixedDeltaTime *= 0.5f;
        StartCoroutine(NoVsyncHandler());
    }

    public static Action<float> slowdownCallback = _ => { };

    public static void SlowdownBy(float by) {
        Slowdown *= by;
        Time.timeScale = Slowdown;
        slowdownCallback(by);
    }

    public static void SlowdownReset() {
        Time.timeScale = 1f;
        float by = 1f / Slowdown;
        Slowdown = 1f;
        slowdownCallback(by);
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
                for (int ii = (int)offset; ii < temp_last; ii += NTHREADS) {
                    DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                    if (!updater.markedForDeletion) updater.obj.RegularUpdateParallel();
                }
                countdown.Signal();
            }, offi);
        }
        countdown.Wait();*/
        
        Profiler.BeginSample("Parallel update");
        Parallel.For(0, updaters.Count, ii => {
            DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
            if (!updater.markedForDeletion) updater.obj.RegularUpdateParallel();
        });
        Profiler.EndSample();
    }

    private void Update() {
        InputManager.OncePerFrameToggleControls();
        //Updates still go out on loading. Player movement is disabled but other things need to run
        noSlowDT = ASSUME_SCREEN_FRAME_TIME;
        if (GameStateManager.IsLoadingOrPaused) {
            //Send out limited updates ignoring slowdown
            for (; noSlowDT > FRAME_BOUNDARY; ) {
                noSlowDT -= FRAME_TIME;
                LastUpdateForScreen = noSlowDT <= FRAME_BOUNDARY;
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                    if (!updater.markedForDeletion && updater.obj.UpdateDuringPause) updater.obj.RegularUpdate();
                }
                updaters.Compact();
            }
            noSlowDT = 0;
        } else {
            if (untilNextRegularFrame + dT > FRAME_BOUNDARY) {
                for (; untilNextRegularFrame + dT > FRAME_BOUNDARY; ) {
                    FrameNumber++;
                    untilNextRegularFrame -= FRAME_TIME;
                    LastUpdateForScreen = untilNextRegularFrame + dT <= FRAME_BOUNDARY;
                    StartOfFrameInvokes();
                    //Note: The updaters array is only modified by this command. 
                    FlushUpdaterAdds();
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
                    EndOfFrameInvokes();
                }
            } else {
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    DeletionMarker<IRegularUpdater> updater = updaters.arr[ii];
                    if (!updater.markedForDeletion && updater.obj.ReceivePartialUpdates) updater.obj.PartialUpdate(dT);
                    updaters.Compact();
                }
            }
            untilNextRegularFrame += dT;
            if (Mathf.Abs(untilNextRegularFrame) < FRAME_YIELD) untilNextRegularFrame = 0f;
        }
        GameStateManager.UpdateGameState();
    }
    private IEnumerator NoVsyncHandler() {
        while (true) {
            yield return new WaitForEndOfFrame();
            lastFrameTime += ASSUME_SCREEN_FRAME_TIME;
            if (ThreadWaiter) {
                Profiler.BeginSample("No-Vsync Synchronizer");
                var sleepTime = lastFrameTime - Time.realtimeSinceStartup - 0.01f;
                if (sleepTime > 0) Thread.Sleep((int)(sleepTime * 1000));
                while (Time.realtimeSinceStartup < lastFrameTime) { }
                Profiler.EndSample();
            }
        }
    }
    
    
    private static readonly CountdownEvent countdown = new CountdownEvent(1);
    private const int NTHREADS = 8;
    private const int PARALLELCUTOFF = 128;

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
    public static void QueueDelayedEOFInvoke(int frame_delay, Action act) => delayedeofInvokes.Enqueue((frame_delay, act));

    private static readonly Queue<DeletionMarker<IRegularUpdater>> updaterAddQueue = new Queue<DeletionMarker<IRegularUpdater>>();

    private static void FlushUpdaterAdds() {
        while (updaterAddQueue.Count > 0) {
            updaters.AddPriority(updaterAddQueue.Dequeue());
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
        /// <summary>
        /// Frame counter, with multiplier built-in.
        /// </summary>
        private float frames = 0f;
        private float seconds => frames * FRAME_TIME;
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
        public void Restart(float mult=1f) {
            frames = 0;
            multiplier = mult;
            enabled = true;
        }
        private void Stop() {
            enabled = false;
        }

        public int UpdatePriority => UpdatePriorities.SYSTEM;
        public bool ReceivePartialUpdates => false;
        public bool UpdateDuringPause => false;
        public void PartialUpdate(float dT) => throw new NotImplementedException();

        public void RegularUpdateParallel() {}

        public void RegularUpdate() {
            if (enabled) frames += multiplier;
        }

        private static readonly Dictionary<string, Timer> timerMap = new Dictionary<string, Timer>();
        //SM-viewable functions
        public static void Start(Timer timer, float multiplier=1f) {
            timer.Start(multiplier);
        }
        public static void Restart(Timer timer, float multiplier=1f) {
            timer.Restart(multiplier);
        }
        public static void Stop(Timer timer) {
            timer.Stop();
        }

        private Timer(string name) {
            this.name = name;
            token = RegisterRegularUpdater(this);
        }

        //TODO verify deregistration, also block SMs from crossing scenes if deregisterd
        void IRegularUpdater.PreSceneClose() {
            token.MarkForDeletion();
            timerMap.Remove(name);
        }

        private readonly string name;
        private readonly DeletionMarker<IRegularUpdater> token;
        public static Timer GetTimer(string name) {
            if (!timerMap.TryGetValue(name, out Timer t)) {
                t = timerMap[name] = new Timer(name);
            }
            return t;
        }

        public static void ResetAll() {
            foreach (var v in timerMap.Values.ToArray()) {
                v.Restart();
                v.Stop();
            }
        }

        public Expression exFrames => Expression.PropertyOrField(Expression.Constant(this), "frames");
        public Expression exSeconds => Expression.PropertyOrField(Expression.Constant(this), "seconds");
    }
    
    
    #if UNITY_EDITOR

    public static void FlushRegularUpdate() {
        updaters.ForEachIfNotCancelled(x => {
            x.RegularUpdateParallel();
            x.RegularUpdate();
        });
    }

#endif
}
