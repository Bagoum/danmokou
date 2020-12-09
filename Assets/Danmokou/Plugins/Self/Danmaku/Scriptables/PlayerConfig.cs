using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Scriptables {
/// <summary>
/// Provides stage metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Player Configuration")]
public class PlayerConfig : ScriptableObject {
    public float freeSpeed = 5f;
    public float focusSlowdown = 0.44f;
    public float hitboxRadius = 0.034f;
    public float grazeboxRadius = 0.3f;
    public float FocusSpeed => freeSpeed * focusSlowdown;
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
    //public ShotConfig[] shots;
    public OrdinalShot[] shots2;

    public Color uiColor;
    public GameObject shotDisplay;
}

[Serializable]
public struct OrdinalShot {
    public string ordinal;
    public ShotConfig shot;
}
}