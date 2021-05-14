using Danmokou.Core;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine;

namespace Danmokou.Scriptables {
/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Stage Configuration")]
public class StageConfig : ScriptableObject, IStageConfig {
    public SceneConfig sceneConfig = null!;
    public TextAsset? stateMachine;
    public string description = "";
    public string stageNumber = "";
    public string defaultSuicideStyle = "";
    public bool practiceable = true;

    public StateMachine? StateMachine => StateMachineManager.FromText(stateMachine);
    public string DefaultSuicideStyle => defaultSuicideStyle;
}
}
