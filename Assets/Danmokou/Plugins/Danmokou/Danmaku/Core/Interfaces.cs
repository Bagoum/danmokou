using System;
using Danmokou.Scriptables;
using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Core {

public interface IStageConfig {
    SceneConfig Scene { get; }
    StateMachine StateMachine { get; }
    string DefaultSuicideStyle { get; }
}

public record EndcardStageConfig(TextAsset stateMachine, SceneConfig Scene) : IStageConfig {
    public StateMachine StateMachine => StateMachineManager.FFromText(stateMachine);
    public string DefaultSuicideStyle => "";
}

public interface ITransformHandler {
    Vector2 Location { get; }
    bool Parented { get; }
    //Note: if !HasParent, then LocalPosition=GlobalPosition
}
}