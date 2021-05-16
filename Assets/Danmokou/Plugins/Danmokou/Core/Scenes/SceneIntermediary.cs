using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Danmokou.Scenes {
/// <summary>
/// Provides functions for scene transitions.
/// </summary>
public static class SceneIntermediary {
    public static bool IsFirstScene { get; private set; } = true;

    public readonly struct SceneRequest {
        public readonly SceneConfig scene;
        public readonly Action? onQueued;
        public readonly Action? onPreLoad;
        public readonly Action? onLoaded;
        public readonly Action? onFinished;
        public readonly Reason reason;

        public enum Reason {
            RELOAD,
            START_ONE,
            RUN_SEQUENCE,
            ENDCARD,
            ABORT_RETURN,
            FINISH_RETURN
        }

        /// <param name="sc"></param>
        /// <param name="reason"></param>
        /// <param name="onPreLoad">Executed when the screen is hidden by the transition effect, before the scene is loaded.</param>
        /// <param name="onLoad">Executed immediately after the scene is loaded. Probably does not precede Awake calls.</param>
        /// <param name="onFinish">Executed when the transition effect is pulled back and standard flow is reenabled.</param>
        public SceneRequest(SceneConfig sc, Reason reason, Action? onPreLoad = null, Action? onLoad = null, 
            Action? onFinish = null) {
            scene = sc;
            onQueued = null;
            this.onPreLoad = onPreLoad;
            onLoaded = onLoad;
            onFinished = onFinish;
            this.reason = reason;
        }

        public override string ToString() => $"{scene.sceneName} ({reason})";
    }

    private static CameraTransitionConfig defaultTransition = null!;

    public static void Setup(CameraTransitionConfig dfltTransition) {
        defaultTransition = dfltTransition;
    }


    public static bool LoadScene(SceneRequest req) {
        if (!EngineStateManager.IsLoading && !LOADING) {
            Log.Unity($"Successfully requested scene load for {req}.");
            req.onQueued?.Invoke();
            IsFirstScene = false;
            LOADING = true;
            EngineStateManager.SetLoading(true, null);
            SceneLoader.Main.RunRIEnumerator(WaitForSceneLoad(req, true));
            return true;
        } else Log.Unity($"REJECTED scene load for {req}.");
        return false;
    }

    //Use a bool here since GameStateManager is updated at end of frame.
    //We need to keep track of whether or not this process has been queued
    public static bool LOADING { get; private set; } = false;

    private static IEnumerator WaitForSceneLoad(SceneRequest req, bool transitionOnSame) {
        var currScene = SceneManager.GetActiveScene().name;
        float waitOut = 0f;
        if (transitionOnSame || currScene != req.scene.sceneName) {
            var transition = req.scene.transitionIn == null ? defaultTransition : req.scene.transitionIn;
            CameraTransition.Fade(transition, out float waitIn, out waitOut);
            Log.Unity($"Performing fade transition for {waitIn}s before loading scene.");
            for (; waitIn > ETime.FRAME_YIELD; waitIn -= ETime.FRAME_TIME) yield return null;
        }
        Log.Unity($"Scene loading for {req} started.", level: Log.Level.DEBUG3);
        StaticPreSceneUnloaded();
        req.onPreLoad?.Invoke();
        var op = SceneManager.LoadSceneAsync(req.scene.sceneName);
        while (!op.isDone) {
            yield return null;
        }
        Log.Unity(
            $"Unity finished loading the new scene. Waiting for transition ({waitOut}s) before yielding control to player.",
            level: Log.Level.DEBUG3);
        req.onLoaded?.Invoke();
        for (; waitOut > ETime.FRAME_YIELD; waitOut -= ETime.FRAME_TIME) yield return null;
        req.onFinished?.Invoke();
        EngineStateManager.SetLoading(false, () => LOADING = false);
    }

    private static readonly List<Action> sceneLoadDelegates = new List<Action>();
    private static readonly List<Action> sceneUnloadDelegates = new List<Action>();
    private static readonly List<Action> presceneUnloadDelegates = new List<Action>();

    private static void StaticSceneLoaded(Scene s, LoadSceneMode lsm) {
        //Log.Unity("Static scene loading procedures (invoked by Unity)");
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
}
