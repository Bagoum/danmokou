using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using SM;
using UnityEngine;

public interface IStageConfig {
    StateMachine StateMachine { get; }
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

    public StateMachine StateMachine => StateMachineManager.FromText(stateMachine);
    public string DefaultSuicideStyle => defaultSuicideStyle;
}