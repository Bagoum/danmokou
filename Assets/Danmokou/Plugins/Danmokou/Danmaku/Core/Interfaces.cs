using Danmokou.Scriptables;
using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Core {

public interface IStageConfig {
    SceneConfig Scene { get; }
    EFStateMachine? StateMachine { get; }
    string DefaultSuicideStyle { get; }
}

public record EndcardStageConfig(string dialogueKey, SceneConfig Scene) : IStageConfig {
    public EFStateMachine StateMachine => new(null, SMReflection.Dialogue(dialogueKey));
    public string DefaultSuicideStyle => "";
}

public interface ITransformHandler {
    Vector2 LocalPosition();
    Vector2 GlobalPosition();
    bool HasParent();
    //Note: if !HasParent, then LocalPosition=GlobalPosition
}
}