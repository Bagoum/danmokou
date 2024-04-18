using System.Collections;
using System.Collections.Generic;
using Danmokou.Scenes;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
/// <summary>
/// Provides metadata about scenes.
/// </summary>
[CreateAssetMenu(menuName = "Data/Scene Configuration")]
public class SceneConfig : ScriptableObject, ISceneConfig {
    public string sceneName = "";
    public CameraTransitionConfig? transitionIn;

    string ISceneConfig.SceneName => sceneName;
    CameraTransitionConfig? ISceneConfig.TransitionIn => transitionIn;
}
}