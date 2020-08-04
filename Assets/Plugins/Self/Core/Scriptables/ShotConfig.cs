using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Shot Configuration")]
public class ShotConfig : ScriptableObject {
    public string title;
    public string description;
    public GameObject prefab;
}