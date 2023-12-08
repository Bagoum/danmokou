using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
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
/// <br/>RegularUpdate functions are called on all objects in the following stages:
/// <br/>- <see cref="RegularUpdateParallel"/> (may be parallelized) 
/// <br/>- <see cref="RegularUpdate"/>
/// <br/>- <see cref="RegularUpdateCollision"/>
/// <br/>- <see cref="RegularUpdateFinalize"/>
/// </summary>
public interface IRegularUpdater {
    /// <summary>
    /// Perform a regular update. This function will be called every ONE engine frame,
    /// with a deltaTime of TimeManagement.FRAME_TIME. This function may be called in parallel,
    /// and may not have access to the Unity thread context.
    /// This function is called before <see cref="RegularUpdate"/>.
    /// </summary>
    void RegularUpdateParallel() { }

    /// <summary>
    /// Perform a regular update. This function will be called every ONE engine frame,
    /// with a deltaTime of TimeManagement.FRAME_TIME. This function is called sequentially,
    /// and has access to the Unity thread context.
    /// </summary>
    void RegularUpdate();

    /// <summary>
    /// Perform a regular update.
    /// <br/>This function is called after all updaters have called <see cref="RegularUpdate()"/>, and before any have called <see cref="RegularUpdateFinalize"/>.
    /// <br/>This function should contain computations that require updated information from multiple updaters (such as collision). In practice, it should compute all collisions where this object's hitbox overlaps any other object's hurtbox.
    /// </summary>
    void RegularUpdateCollision() { }

    /// <summary>
    /// Perform a regular update.
    /// <br/>This function is called after all updaters have called <see cref="RegularUpdate()"/> and <see cref="RegularUpdateCollision"/>.
    /// <br/>This function should contain computations that require knowledge of all collision information, such as rendering computations.
    /// </summary>
    void RegularUpdateFinalize() { }

    /// <summary>
    /// This function is called at the *end* of the engine frame that an object was registered
    /// or re-registered for regular updates.
    /// <br/>In the standard case, this means that it is called on the same frame as Awake, but after all Awake calls.
    ///  RegularUpdate will be called starting on the next engine frame.
    /// <br/>Works with pooled objects (will be called after ResetV). Similar to Unity's Start.
    /// </summary>
    void FirstFrame() { }

    /// <summary>
    /// True iff <see cref="RegularUpdateParallel"/> is nontrivial. If there are enough nontrivial updaters,
    ///  then <see cref="RegularUpdateParallel"/> will be parallelized during the update step.
    /// </summary>
    bool HasNontrivialParallelUpdate => false;

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
    public const int SYSTEM = -80;
    public const int UI = -60;
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
    private static ETime Main { get; set; } = null!;
    public static float ASSUME_SCREEN_FRAME_TIME { get; private set; } = 1 / 60f;
    private static bool GameTimeIsPaused => EngineStateManager.State > EngineState.RUN;
    private float untilNextRegularFrame = 0f;
    private float untilNextPauseFrame = 0f;
    private float UntilNextFrame {
        get => GameTimeIsPaused ? untilNextPauseFrame : untilNextRegularFrame;
        set {
            if (GameTimeIsPaused)
                untilNextPauseFrame = value;
            else
                untilNextRegularFrame = value;
        }
    }
    
    /// <summary>
    /// Process the provided amount of time instantaneously.
    /// </summary>
    public static void SkipTime(float t) => Main.untilNextRegularFrame += t;
    
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
    public static float dT => GameTimeIsPaused ? 0 : (ASSUME_SCREEN_FRAME_TIME * Slowdown.Value);
    private static float EngineStepTime =>
        GameTimeIsPaused ? ASSUME_SCREEN_FRAME_TIME : (ASSUME_SCREEN_FRAME_TIME * Slowdown.Value);
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
    private static readonly DMCompactingArray<IRegularUpdater> updaters = new(256);
    private static readonly Queue<(Action, EngineState)> eofInvokes = new Queue<(Action, EngineState)>();
    private static readonly DMCompactingArray<(Action cb, EngineState state)> persistentEofInvokes = new();
    private static readonly DMCompactingArray<(Action cb, EngineState state)> persistentSofInvokes = new();
    private static readonly DMCompactingArray<(Action cb, EngineState state)> persistentUnitySofInvokes = new();

    private void Awake() {
        Main = this;
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
        UnityTimeRate.AddDisturbance(EngineStateManager.EvState.Map(s => s.Timescale()));
        UnityTimeRate.Subscribe(s => Time.timeScale = s);
        Physics2D.simulationMode = SimulationMode2D.Script;

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
        SetForcedFPS((int)Math.Round(Screen.currentResolution.refreshRateRatio.value));
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
        /*Profiler.BeginSample("Partitioner");
        var p = Partitioner.Create(0, updaters.Count);
        Profiler.EndSample();*/
        Profiler.BeginSample("Parallel update");
        /*Parallel.ForEach(p, range => {
            for (int ii = range.Item1; ii < range.Item2; ++ii)
                if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                    u.RegularUpdateParallel();
        });*/
        Parallel.For(0, updaters.Count, ii => {
            if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                u.RegularUpdateParallel();
        });
        Profiler.EndSample();
        RNG.RNG_ALLOWED = true;
    }

    private void Update() {
        try {
            FirstUpdateForScreen = true;
            UnityStartOfFrameInvokes(EngineStateManager.State);
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
                //This is important for cases where objects are added at the end of the previous frame
                // and then scanned (for player collision) during the movement step
                Physics2D.SyncTransforms();
                Profiler.BeginSample("Regular Update: (1) Parallel Step");
                //Parallelize updates if there are many. Note that this allocates several kb
                if (updaters.Count > PARALLELCUTOFF) {
                    int heavyUpdates = 0;
                    for (int ii = 0; ii < updaters.Count; ++ii)
                        if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State &&
                            u.HasNontrivialParallelUpdate)
                            ++heavyUpdates;
                    if (heavyUpdates > PARALLELCUTOFF) {
                        ParallelUpdateStep();
                        goto normal_step;
                    }
                }
                for (int ii = 0; ii < updaters.Count; ++ii)
                    if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                        u.RegularUpdateParallel();
                normal_step: ;
                Profiler.EndSample();
                Profiler.BeginSample("Regular Update: (2) Normal Step");
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                        u.RegularUpdate();
                }
                Profiler.EndSample();
                Profiler.BeginSample("Regular Update: (2.5) Unity Physics Simulation");
                Physics2D.Simulate(FRAME_TIME);
                Profiler.EndSample();
                Profiler.BeginSample("Regular Update: (3) Collision Step");
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                        u.RegularUpdateCollision();
                }
                Profiler.EndSample();
                Profiler.BeginSample("Regular Update: (4) Finalize Step");
                for (int ii = 0; ii < updaters.Count; ++ii) {
                    if (updaters.GetIfExistsAt(ii, out var u) && u.UpdateDuring >= EngineStateManager.State)
                        u.RegularUpdateFinalize();
                }
                Profiler.EndSample();
                updaters.Compact();
                EndOfFrameInvokes(EngineStateManager.State);
                //Note: The updaters array is only modified by this command. 
                FlushUpdaterAdds();
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
        var eof = new WaitForEndOfFrame();
        while (true) {
            yield return eof;
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

    private static void UnityStartOfFrameInvokes(EngineState state) {
        for (int ii = 0; ii < persistentUnitySofInvokes.Count; ++ii)
            if (persistentUnitySofInvokes.GetIfExistsAt(ii, out var x) && x.state >= state)
                x.cb();
        persistentSofInvokes.Compact();
    }
    private static void StartOfFrameInvokes(EngineState state) {
        for (int ii = 0; ii < persistentSofInvokes.Count; ++ii)
            if (persistentSofInvokes.GetIfExistsAt(ii, out var x) && x.state >= state)
                x.cb();
        persistentSofInvokes.Compact();
    }

    private static void EndOfFrameInvokes(EngineState state) {
        for (int ii = 0; ii < persistentEofInvokes.Count; ++ii) 
            if (persistentEofInvokes.GetIfExistsAt(ii, out var x) && x.state >= state)
                x.cb();
        persistentEofInvokes.Compact();
        
        var neofInv = eofInvokes.Count;
        for (int ii = 0; ii < neofInv; ++ii) {
            var (act, st) = eofInvokes.Dequeue();
            if (st >= state)
                act();
            else
                eofInvokes.Enqueue((act, st));
        }
    }

    public static IDisposable RegisterPersistentUnitySOFInvoke(Action act, EngineState state = EngineState.RUN) => 
        persistentUnitySofInvokes.Add((act, state));
    public static IDisposable RegisterPersistentSOFInvoke(Action act, EngineState state = EngineState.RUN) => 
        persistentSofInvokes.Add((act, state));
    public static IDisposable RegisterPersistentEOFInvoke(Action act, EngineState state = EngineState.RUN) => 
        persistentEofInvokes.Add((act, state));
    public static void QueueEOFInvoke(Action act, EngineState state = EngineState.RUN) => eofInvokes.Enqueue((act, state));

    private static readonly DMCompactingArray<IRegularUpdater> updaterAddQueue =
        new(256);
    private static readonly List<DeletionMarker<IRegularUpdater>> updatersToAdd_temp = new();

    private static void FlushUpdaterAdds() {
        //FirstFrame may itself cause new objects to be added, so we need to
        // handle adds with this fixed-point approach
        while (updaterAddQueue.Count > 0) {
            updatersToAdd_temp.Clear();
            updaterAddQueue.CopyIntoList(updatersToAdd_temp);
            updaterAddQueue.Empty();
            foreach (var iru in updatersToAdd_temp) {
                iru.Value.FirstFrame();
                updaters.AddPriority(iru);
            }
        }
    }

    public static DeletionMarker<IRegularUpdater> RegisterRegularUpdater(IRegularUpdater iru) {
        var dm = updaterAddQueue.AddPriority(iru, iru.UpdatePriority);
        return dm;
    }


    /// <summary>
    /// Script and FF-viewable stopwatches that move with game time.
    /// </summary>
    public class Timer : IRegularUpdater {
        private static readonly Dictionary<string, Timer> timerMap = new Dictionary<string, Timer>();
        public static Timer PhaseTimer => GetTimer("phaset");
        private DeletionMarker<IRegularUpdater>? token;
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
        
        public int UpdatePriority => UpdatePriorities.SYSTEM;
        public EngineState UpdateDuring => EngineState.RUN;
        
        private Timer(string name) {
            this.name = name;
        }

        private void Start(float mult) {
            multiplier = mult;
            token ??= RegisterRegularUpdater(this);
        }

        public void Restart(float mult = 1f) {
            Frames = 0;
            Start(mult);
        }

        private void Stop() {
            token?.MarkForDeletion();
            token = null;
        }


        public void RegularUpdate() {
            Profiler.BeginSample("Timer update");
            Frames += multiplier;
            Profiler.EndSample();
        }

        public void FirstFrame() { }

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

        public static Timer GetTimer(string name) {
            if (!timerMap.TryGetValue(name, out Timer t)) {
                t = timerMap[name] = new Timer(name);
            }
            return t;
        }
        
        /// <summary>
        /// Stop all executing timers.
        /// </summary>
        public static void StopAll() {
            foreach (var v in timerMap.Values)
                v.Stop();
        }

        public Expression exFrames => Expression.PropertyOrField(Expression.Constant(this), nameof(Frames));
        public Expression exSeconds => Expression.PropertyOrField(Expression.Constant(this), nameof(Seconds));

        public override string ToString() => $"Timer {name}";
    }

    public void OnDestroy() {
        Logs.CloseLog();
    }
    
    [ContextMenu("Debug Updaters")]
    public void DebugUpdaters() {
        var sb = new StringBuilder();
        updaters.Compact();
        sb.Append($"Updaters: {updaters.Count}\n");
        var byType = new Dictionary<Type, int>();
        foreach (var u in updaters) {
            var t = u.GetType();
            byType[t] = byType.TryGetValue(t, out var ct) ? ct + 1 : 1;
        }
        foreach (var k in byType.Keys.OrderByDescending(x => byType[x]))
            sb.Append($"{k.RName()}: {byType[k]}\n");
        Logs.Log(sb.ToString());
    }
}
}
