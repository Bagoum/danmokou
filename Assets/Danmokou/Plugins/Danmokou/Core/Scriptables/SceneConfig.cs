using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
/// <summary>
/// Provides metadata about scenes.
/// </summary>
[CreateAssetMenu(menuName = "Data/Scene Configuration")]
public class SceneConfig : ScriptableObject {
    public string sceneName = "";
    public CameraTransitionConfig? transitionIn;
}
}