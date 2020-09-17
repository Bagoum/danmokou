using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Provides centralized scene information to teleporter objects.
/// </summary>
[CreateAssetMenu(menuName = "Data/Scene Configuration")]
public class SceneConfig : ScriptableObject {
    public string sceneName;
    [CanBeNull] public CameraTransitionConfig transitionIn;
}