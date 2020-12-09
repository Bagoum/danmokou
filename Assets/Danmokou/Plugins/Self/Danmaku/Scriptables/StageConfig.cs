using DMK.Core;
using JetBrains.Annotations;
using DMK.SM;
using UnityEngine;

namespace DMK.Scriptables {
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
}
