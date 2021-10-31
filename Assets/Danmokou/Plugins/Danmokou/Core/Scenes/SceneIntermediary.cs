using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Danmokou.Scenes {
/// <summary>
/// Manages scene transitions.
/// </summary>
public class SceneIntermediary : CoroutineRegularUpdater, ISceneIntermediary {
    public static bool IsFirstScene { get; private set; } = true;
    //Use a bool here since EngineStateManager is updated at end of frame.
    //We need to keep track of whether or not this process has been queued
    public static bool LOADING { get; private set; } = false;

    public CameraTransitionConfig defaultTransition = null!;
    private Cancellable sceneToken = new Cancellable();
    public ICancellee SceneBoundedToken => sceneToken;

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ISceneIntermediary>(this, new ServiceLocator.ServiceOptions { Unique = true });
        Listen(PreSceneUnload, () => {
            sceneToken.Cancel();
            sceneToken = new Cancellable();
        });
    }

    public bool LoadScene(SceneRequest req) {
        if (EngineStateManager.State < EngineState.LOADING_PAUSE && !LOADING) {
            Logs.Log($"Successfully requested scene load for {req}.");
            req.onQueued?.Invoke();
            IsFirstScene = false;
            LOADING = true;
            var stateToken = EngineStateManager.RequestState(EngineState.LOADING_PAUSE);
            RunRIEnumerator(WaitForSceneLoad(stateToken, req, true));
            return true;
        } else Logs.Log($"REJECTED scene load for {req}.");
        return false;
    }


    private IEnumerator WaitForSceneLoad(IDisposable stateToken, SceneRequest req, bool transitionOnSame) {
        var currScene = SceneManager.GetActiveScene().name;
        float waitOut = 0f;
        if (transitionOnSame || currScene != req.scene.sceneName) {
            var transition = req.scene.transitionIn == null ? defaultTransition : req.scene.transitionIn;
            float waitIn = 0f;
            ServiceLocator.MaybeFind<ICameraTransition>()?.Fade(transition, out waitIn, out waitOut);
            Logs.Log($"Performing fade transition for {waitIn}s before loading scene.");
            for (; waitIn > ETime.FRAME_YIELD; waitIn -= ETime.FRAME_TIME) yield return null;
        }
        Logs.Log($"Scene loading for {req} started.", level: LogLevel.DEBUG3);
        PreSceneUnload.OnNext(default);
        req.onPreLoad?.Invoke();
        var op = SceneManager.LoadSceneAsync(req.scene.sceneName);
        while (!op.isDone) {
            yield return null;
        }
        Logs.Log(
            $"Unity finished loading the new scene. Waiting for transition ({waitOut}s) before yielding control to player.",
            level: LogLevel.DEBUG3);
        req.onLoaded?.Invoke();
        for (; waitOut > ETime.FRAME_YIELD; waitOut -= ETime.FRAME_TIME) yield return null;
        req.onFinished?.Invoke();
        stateToken.Dispose();
        LOADING = false;
    }
    
    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SOF;
    

    //Static stuff
    public static Event<Unit> PreSceneUnload { get; } = new Event<Unit>();
    public static Event<Unit> SceneUnloaded { get; } = new Event<Unit>();
    public static Event<Unit> SceneLoaded { get; } = new Event<Unit>();

    public static void Attach() {
        SceneManager.sceneUnloaded += s => {
            Logs.Log($"Unity scene {s.name} was unloaded");
            SceneUnloaded.OnNext(default);
        };
        SceneManager.sceneLoaded += (s, m) => {
            Logs.Log($"Unity scene {s.name} was loaded via mode {m.ToString()}");
            SceneLoaded.OnNext(default);
        };
    }
}
}
