using System;
using JetBrains.Annotations;

namespace Danmaku {
public class LevelController : BehaviorEntity {
    public IStageConfig stage;
    public StageConfig wip_stage;
    private string _DefaultSuicideStyle => stage?.DefaultSuicideStyle;
    public static string DefaultSuicideStyle { get; private set; }
    public override bool TriggersUITimeout => true;

    public enum LevelRunMethod {
        SINGLE,
        CONTINUE
    }
    public readonly struct LevelRunRequest {
        public readonly int toPhase;
        [CanBeNull] public readonly Action cb;
        public readonly LevelRunMethod method;
        public readonly IStageConfig stage;

        public LevelRunRequest(int phase, [CanBeNull] Action callback, LevelRunMethod runMethod, IStageConfig stageConf) {
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
        behaviorScript = null;
#if UNITY_EDITOR
        if (SceneIntermediary.IsFirstScene && wip_stage != null) {
            Log.Unity("Running default level controller script under editor first-scene conditions");
            //Only run the default stage under editor testing conditions
            behaviorScript = wip_stage.stateMachine;
        }
#endif
        base.Awake();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterDI(this);
    }

    protected override void OnDisable() {
        DefaultSuicideStyle = null;
        base.OnDisable();
    }
}
}