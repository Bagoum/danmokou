using System;
using BagoumLib.Cancellation;
using Danmokou.Scriptables;

namespace Danmokou.Scenes {
public interface ISceneIntermediary {
    ICancellee SceneBoundedToken { get; }
    bool LoadScene(SceneRequest req);
}
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
}