using System.Collections;
using System.Collections.Generic;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Shot Configuration")]
public class ShotConfig : ScriptableObject {
    public string key;
    /// <summary>
    /// eg. "Homing Needles - Persuasion Laser"
    /// </summary>
    public string title;
    public string description;
    public GameObject prefab;
    public PlayerBombType bomb;
    public bool HasBomb => bomb.IsValid();
    public double defaultPower = 1000;
}