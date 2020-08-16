using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Provides functions for scene transitions.
/// </summary>
public static class SceneIntermediary {
    public static bool IsFirstScene { get; private set; } = true;

    public readonly struct SceneRequest {
        public readonly SceneConfig scene;
        [CanBeNull] public readonly Action onQueued;
        [CanBeNull] public readonly Action onLoaded;
        [CanBeNull] public readonly Action onFinished;
        public readonly bool reload;
        public readonly float delay;

        public SceneRequest(SceneConfig sc, [CanBeNull] Action onQueue, [CanBeNull] Action onLoad,
            [CanBeNull] Action onFinish, bool isReload = false, float? delay = null) {
            scene = sc;
            onQueued = onQueue;
            onLoaded = onLoad;
            onFinished = onFinish;
            reload = isReload;
            this.delay = delay ?? 0f;
        }

        public override string ToString() => scene.sceneName + (reload ? " (Reload)" : "");
    }

    private static SceneRequest? LastSceneRequest = null;

    /// <summary>
    /// Use GameManagement.ReloadScene instead.
    /// </summary>
    public static bool _ReloadScene(Action onLoaded) {
        if (LastSceneRequest != null) {
            var lsr = LastSceneRequest.Value;
            return LoadScene(new SceneRequest(lsr.scene, null, lsr.onLoaded + onLoaded, lsr.onFinished, true));
        }
        return false;
    }

    public static bool LoadScene(SceneConfig sc, [CanBeNull] Action onQueued=null, float? delay = null) => 
        LoadScene(new SceneRequest(sc, onQueued, null, null, delay: delay));
    public static bool LoadScene(SceneRequest req) {
        if (!GameStateManager.IsLoading && !LOADING) {
            Log.Unity($"Successfully requested scene load for {req}.");
            req.onQueued?.Invoke();
            IsFirstScene = false;
            LastSceneRequest = req;
            LOADING = true;
            CoroutineRegularUpdater.GlobalDuringPause.RunRIEnumerator(WaitForSceneLoad(req, true));
            return true;
        } else Log.Unity($"REJECTED scene load for {req}.");
        return false;
    }

    //Use a bool here since GameStateManager is updated at end of frame.
    //We need to keep track of whether or not this process has been queued
    public static bool LOADING { get; private set; } = false;
    private static IEnumerator WaitForSceneLoad(SceneRequest req, bool transitionOnSame) {
        var currScene = SceneManager.GetActiveScene().name;
        if (req.delay > 0) Log.Unity($"Performing delay for {req.delay}s before loading scene.");
        for (float t = req.delay; t > ETime.FRAME_YIELD; t -= ETime.FRAME_TIME) yield return null;
        float waitOut = 0f;
        GameStateManager.SetLoading(true);
        if (transitionOnSame || currScene != req.scene.sceneName) {
            CameraTransition.Fade(req.scene.transitionIn, out float waitIn, out waitOut);
            Log.Unity($"Performing fade transition for {waitIn}s before loading scene.");
            for (; waitIn > ETime.FRAME_YIELD; waitIn -= ETime.FRAME_TIME) yield return null;
        }
        Log.Unity($"Scene loading for {req} started.", level: Log.Level.DEBUG3);
        StaticPreSceneUnloaded();
        var op = SceneManager.LoadSceneAsync(req.scene.sceneName);
        while (!op.isDone) {
            yield return null;
        }
        Log.Unity($"Scene loading processed. Waiting for transition ({waitOut}s) before yielding control to player.", level: Log.Level.DEBUG3);
        req.onLoaded?.Invoke();
        for (; waitOut > ETime.FRAME_YIELD; waitOut -= ETime.FRAME_TIME) yield return null;
        LOADING = false;
        GameStateManager.SetLoading(false);
        req.onFinished?.Invoke();
    }

    private static readonly List<Action> sceneLoadDelegates = new List<Action>();
    private static readonly List<Action> sceneUnloadDelegates = new List<Action>();
    private static readonly List<Action> presceneUnloadDelegates = new List<Action>();
    private static void StaticSceneLoaded(Scene s, LoadSceneMode lsm) {
        Log.Unity("Static scene loading procedures (invoked by Unity)");
        for (int ii = 0; ii < sceneLoadDelegates.Count; ++ii) {
            sceneLoadDelegates[ii]();
        }
    }
    private static void StaticSceneUnloaded(Scene s) {
        for (int ii = 0; ii < sceneUnloadDelegates.Count; ++ii) {
            sceneUnloadDelegates[ii]();
        }
    }
    private static void StaticPreSceneUnloaded() {
        for (int ii = 0; ii < presceneUnloadDelegates.Count; ++ii) {
            presceneUnloadDelegates[ii]();
        }
    }

    public static void RegisterSceneLoad(Action act) {
        sceneLoadDelegates.Add(act);
    }
    public static void RegisterSceneUnload(Action act) {
        sceneUnloadDelegates.Add(act);
    }
    public static void RegisterPreSceneUnload(Action act) {
        presceneUnloadDelegates.Add(act);
    }

    //Invoked by ETime
    public static void Attach() {
        SceneManager.sceneLoaded += StaticSceneLoaded;
        SceneManager.sceneUnloaded += StaticSceneUnloaded;
    }
}