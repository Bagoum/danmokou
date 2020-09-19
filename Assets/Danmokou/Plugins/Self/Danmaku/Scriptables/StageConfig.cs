using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using SM;
using UnityEngine;


public interface IStageConfig {
    TextAsset StateMachine { get; }
    [CanBeNull] SM.StateMachine StateMachineOverride { get; }
    string DefaultSuicideStyle { get; }
}

public class EndcardStageConfig : IStageConfig {
    private readonly string dialogueKey;
    public TextAsset StateMachine => null;
    public StateMachine StateMachineOverride => new ReflectableLASM(SMReflection.Dialogue(dialogueKey));
    public string DefaultSuicideStyle => "";

    public EndcardStageConfig(string dialogueKey) {
        this.dialogueKey = dialogueKey;
    }
}

/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Stage Configuration")]
public class StageConfig : ScriptableObject, IStageConfig {
    public SceneConfig sceneConfig;
    [CanBeNull] public TextAsset stateMachine;
    public string description;
    public string stageNumber;
    public string defaultSuicideStyle;
    public bool practiceable = true;

    public TextAsset StateMachine => stateMachine;
    public StateMachine StateMachineOverride => null;
    public string DefaultSuicideStyle => defaultSuicideStyle;
}