using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Scriptables;

namespace Danmokou.Scenes {
public interface ISceneConfig {
    string SceneName { get; }
    CameraTransitionConfig? TransitionIn { get; }

    public record SC(string SceneName, CameraTransitionConfig? TransitionIn = null) : ISceneConfig;
}
public interface ISceneIntermediary {
    /// <summary>
    /// Start loading the provided scene.
    /// </summary>
    /// <returns>The task that spans the scene loading process (ending when the curtain is pulled back).
    /// This can return null when another scene is already in the process of loading.</returns>
    SceneLoading? LoadScene(SceneRequest req);
}

/// <summary>
/// Container for the tasks produced by the scene loading process.
/// </summary>
/// <param name="Preloading">The task that is completed when the pre-load effect covers the screen.</param>
/// <param name="Loading">The task that is completed when the scene changes (after Awake calls).</param>
/// <param name="Finishing">The task that is completed when standard control flow is reenabled.</param>
public record SceneLoading(TaskCompletionSource<Unit> Preloading, TaskCompletionSource<Unit> Loading, TaskCompletionSource<Unit> Finishing);

/// <summary>
/// Information required when requesting a scene load.
/// </summary>
/// <param name="scene"></param>
/// <param name="reason"></param>
/// <param name="onPreLoad">Executed when the screen is hidden by the transition effect, before the scene is loaded.</param>
/// <param name="onLoaded">Executed immediately after the scene is loaded. Probably does not precede Awake calls.</param>
/// <param name="onFinished">Executed when standard control flow is reenabled. Note that this may happen at any time during the visual detransition.</param>
public record SceneRequest(ISceneConfig scene, SceneRequest.Reason reason, Action? onPreLoad = null, Action? onLoaded = null, 
    Action? onFinished = null) {
    public enum Reason {
        START_ONE,
        RUN_SEQUENCE,
        ABORT_RETURN,
        FINISH_RETURN
    }
    public ICameraTransitionConfig? Transition { get; init; }

    //we can't directly return onLoaded() since onLoaded will get executed at a later point, that's why we 
    // use a TCS for indirection
    public static SceneRequest TaskFromOnLoad<T>(ISceneConfig sc, Reason reason, Action? onPreLoad, Func<Task<T>> onLoaded,
        Action? onFinished, out TaskCompletionSource<T> awaiter) {
        var tcs = awaiter = new TaskCompletionSource<T>();
        return new SceneRequest(sc, reason, onPreLoad, () => onLoaded().Pipe(tcs), onFinished);
    }

    public static SceneRequest TaskFromOnFinish<T>(ISceneConfig sc, Reason reason, Action? onPreLoad, Action? onLoaded,
        Func<Task<T>> onFinished, out TaskCompletionSource<T> awaiter) {
        var tcs = awaiter = new TaskCompletionSource<T>();
        return new SceneRequest(sc, reason, onPreLoad, onLoaded, () => onFinished().Pipe(tcs));
    }

    public SceneRequest With(ICameraTransitionConfig? transition) {
        if (transition is null) return this;
        return this with { Transition = transition };
    }

    public override string ToString() => $"{scene.SceneName} ({reason})";
}
}