using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
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

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ISceneIntermediary>(this, new ServiceLocator.ServiceOptions { Unique = true });
    }

    public SceneLoading? LoadScene(SceneRequest req) {
        if (EngineStateManager.State < EngineState.LOADING_PAUSE && !LOADING) {
            Logs.Log($"Successfully requested scene load for {req}.");
            IsFirstScene = false;
            LOADING = true;
            var stateToken = EngineStateManager.RequestState(EngineState.LOADING_PAUSE);
            var loader = new SceneLoading(new(), new(), new());
            RunRIEnumerator(WaitForSceneLoad(stateToken, req, loader, true));
            return loader;
        } else Logs.Log($"REJECTED scene load for {req}. Current game state is {EngineStateManager.State} " +
                        $"(loading: {LOADING})", true, LogLevel.WARNING);
        return null;
    }


    private IEnumerator WaitForSceneLoad(IDisposable stateToken, SceneRequest req, SceneLoading loader, bool transitionOnSame) {
        var currScene = SceneManager.GetActiveScene().name;
        float waitOut = 0f;
        if (transitionOnSame || currScene != req.scene.SceneName) {
            var transition = req.Transition ?? (req.scene.TransitionIn == null ? defaultTransition : req.scene.TransitionIn);
            ServiceLocator.Find<ICameraTransition>().Fade(transition, out var waitIn, out waitOut);
            Logs.Log($"Performing fade transition for {waitIn}s before loading scene.");
            for (; waitIn > ETime.FRAME_YIELD; waitIn -= ETime.FRAME_TIME) yield return null;
        }
        //Logs.Log($"Scene loading for {req} started.", level: LogLevel.DEBUG1);
        req.onPreLoad?.Invoke();
        loader.Preloading.SetResult(default);
        var op = SceneManager.LoadSceneAsync(req.scene.SceneName);
        while (!op.isDone) {
            yield return null;
        }
        Logs.Log($"The scene loader has finished loading scene {req}. " +
                 $"The out transition will take {waitOut}s, but the scene will start immediately.",
            level: LogLevel.DEBUG3);
        req.onLoaded?.Invoke();
        loader.Loading.SetResult(default);
        req.onFinished?.Invoke();
        loader.Finishing.SetResult(default);
        stateToken.Dispose();
        for (; waitOut > ETime.FRAME_YIELD; waitOut -= ETime.FRAME_TIME) yield return null;
        LOADING = false;
        yield return null;
    }
    
    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SOF;
    

    /// <summary>
    /// Called when the Unity scene has been unloaded.
    /// <br/>GC.Collect is called right after this.
    /// <br/>This is a good time to clear caches.
    /// </summary>
    public static Event<Unit> SceneUnloaded { get; } = new Event<Unit>();
    
    /// <summary>
    /// Called when the Unity scene has been loaded, after all Awake calls.
    /// <br/>This is called on the first scene load as well.
    /// </summary>
    public static Event<Scene> SceneLoaded { get; } = new();

    //this is easier to handle than a static constructor due to rules against accessing
    // unity APIs in static constructors
    public static void Attach() {
        SceneManager.sceneUnloaded += s => {
            Logs.Log($"Unity scene {s.name} was unloaded. Now calling {nameof(SceneUnloaded)} on all listeners.");
            SceneUnloaded.OnNext(default);
            GC.Collect();
        };
        SceneManager.sceneLoaded += (s, m) => {
            Logs.Log($"Unity scene {s.name} was loaded via mode {m.ToString()}. Awake has been called on all live " +
                     $"objects. Now calling {nameof(SceneLoaded)} on all listeners.");
            SceneLoaded.OnNext(s);
        };
    }
}
}
