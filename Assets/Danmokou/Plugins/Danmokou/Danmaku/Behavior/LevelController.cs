using System;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using JetBrains.Annotations;

namespace Danmokou.Behavior {
public class LevelController : BehaviorEntity {
    public IStageConfig? stage;
    public StageConfig? wip_stage;
    private string? _DefaultSuicideStyle => stage?.DefaultSuicideStyle;
    public static string? DefaultSuicideStyle { get; private set; }
    public override bool TriggersUITimeout => true;

    public enum LevelRunMethod {
        SINGLE,
        CONTINUE
    }
    public readonly struct LevelRunRequest {
        public readonly int toPhase;
        public readonly Action? cb;
        public readonly LevelRunMethod method;
        public readonly IStageConfig stage;

        public LevelRunRequest(int phase, Action? callback, LevelRunMethod runMethod, IStageConfig stageConf) {
            toPhase = phase;
            cb = callback;
            method = runMethod;
            stage = stageConf;
        }
    }

    public void Request(LevelRunRequest req) {
        if (req.method == LevelRunMethod.SINGLE) phaseController.Override(req.toPhase, req.cb);
        else if (req.method == LevelRunMethod.CONTINUE) phaseController.SetGoTo(req.toPhase, req.cb);
        stage = req.stage;
        RunPatternSM(req.stage.StateMachine);
    }

    protected override void Awake() {
        DefaultSuicideStyle = stage?.DefaultSuicideStyle;
#if UNITY_EDITOR
        if (SceneIntermediary.IsFirstScene) {
            if (wip_stage != null) {
                Logs.Log("Running default stage under editor first-scene conditions");
                behaviorScript = wip_stage.stateMachine;
            } else if (behaviorScript != null) {
                Logs.Log("Running default level script under editor first-scene conditions");
            }
        } else
            behaviorScript = null;
#else
        behaviorScript = null;
#endif
        base.Awake();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this, new ServiceLocator.ServiceOptions { Unique = true });
    }

    protected override void OnDisable() {
        DefaultSuicideStyle = null;
        base.OnDisable();
    }
}
}