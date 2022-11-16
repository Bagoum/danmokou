using Danmokou.Scriptables;
using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Core {

public interface IStageConfig {
    SceneConfig Scene { get; }
    StateMachine? StateMachine { get; }
    string DefaultSuicideStyle { get; }
}

public record EndcardStageConfig(string dialogueKey, SceneConfig Scene) : IStageConfig {
    public StateMachine StateMachine => new ReflectableLASM(SMReflection.Dialogue(dialogueKey));
    public string DefaultSuicideStyle => "";
}

public interface ITransformHandler {
    Vector2 LocalPosition();
    Vector2 GlobalPosition();
    bool HasParent();
    //Note: if !HasParent, then LocalPosition=GlobalPosition
}
}