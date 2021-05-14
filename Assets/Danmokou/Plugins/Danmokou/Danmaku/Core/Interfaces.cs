using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Core {

public interface IStageConfig {
    StateMachine? StateMachine { get; }
    string DefaultSuicideStyle { get; }
}

public class EndcardStageConfig : IStageConfig {
    private readonly string dialogueKey;
    public StateMachine StateMachine => new ReflectableLASM(SMReflection.Dialogue(dialogueKey));
    public string DefaultSuicideStyle => "";

    public EndcardStageConfig(string dialogueKey) {
        this.dialogueKey = dialogueKey;
    }
}

public interface ITransformHandler {
    Vector2 LocalPosition();
    Vector2 GlobalPosition();
    bool HasParent();
    //Note: if !HasParent, then LocalPosition=GlobalPosition
}
}