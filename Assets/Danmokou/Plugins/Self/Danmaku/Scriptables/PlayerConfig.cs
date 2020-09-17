using System.Collections;
using System.Collections.Generic;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Player Configuration")]
public class PlayerConfig : ScriptableObject {
    public string key;
    /// <summary>
    /// Eg. Mokou (padded to 8)
    /// </summary>
    public string shortTitle;
    /// <summary>
    /// Eg. Fujiwara no Mokou
    /// </summary>
    public string title;
    public GameObject prefab;
    public ShotConfig[] shots;
}