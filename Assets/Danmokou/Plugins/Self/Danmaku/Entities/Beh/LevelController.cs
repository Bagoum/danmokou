using System;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;

namespace Danmaku {
public class LevelController : BehaviorEntity {
    public StageConfig stage;
    private string _DefaultSuicideStyle => (stage == null) ? null : stage.defaultSuicideStyle;
    private static LevelController main;
    public static string DefaultSuicideStyle => (main == null) ? null : main._DefaultSuicideStyle;
    public override bool TriggersUITimeout => true;

    //[Header("On Level Completion")] public RString KVRKey;
    //public RInt KVRValue;
    //public bool overrideKVR = true;

    public enum LevelRunMethod {
        SINGLE,
        CONTINUE
    }
    public readonly struct LevelRunRequest {
        public readonly int toPhase;
        [CanBeNull] public readonly Action cb;
        public readonly LevelRunMethod method;
        public readonly StageConfig stage;

        public LevelRunRequest(int phase, [CanBeNull] Action callback, LevelRunMethod runMethod, StageConfig stageConf) {
            toPhase = phase;
            cb = callback;
            method = runMethod;
            stage = stageConf;
        }
    }

    public static void Request(LevelRunRequest req) {
        if (req.method == LevelRunMethod.SINGLE) main.phaseController.Override(req.toPhase, req.cb);
        else if (req.method == LevelRunMethod.CONTINUE) main.phaseController.SetGoTo(req.toPhase, req.cb);
        main.stage = req.stage;
        main.behaviorScript = req.stage.stateMachine;
        main.RunAttachedSM();
    }

    protected override void Awake() {
        main = this;
        behaviorScript = null;
#if UNITY_EDITOR
        if (SceneIntermediary.IsFirstScene) {
            Log.Unity("Running default level controller script under editor first-scene conditions");
            //Only run the default stage under editor testing conditions
            behaviorScript = (stage == null) ? null : stage.stateMachine;
        }
#endif
        base.Awake();
    }

#if UNITY_EDITOR
    public static LevelController Main => main;
#endif
}
}