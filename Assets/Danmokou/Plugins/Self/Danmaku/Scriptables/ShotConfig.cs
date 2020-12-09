using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using DMK.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Scriptables {
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
    [TextArea(5, 10)] public string description;
    /// <summary>
    /// eg. "Forward Focus"
    /// </summary>
    public string type;
    [Header("Unitary Shot Configuration")] public GameObject prefab;
    public PlayerBombType bomb;
    public bool HasBomb => bomb.IsValid();
    public double defaultPower = 1000;
    public bool playerChild = true;
    [Header("Multi-Shot Configuration")] public bool isMultiShot;
    public ShotConfig multiD;
    public ShotConfig multiM;
    public ShotConfig multiK;
    public SFXConfig onSwap;
    [CanBeNull]
    public IEnumerable<ShotConfig> Subshots => isMultiShot ?
        new[] {multiD, multiM, multiK} :
        null;

    public ShotConfig GetSubshot(Subshot sub) {
        if (!isMultiShot) return this;
        else
            switch (sub) {
                case Subshot.TYPE_D:
                    return multiD;
                case Subshot.TYPE_M:
                    return multiM;
                case Subshot.TYPE_K:
                    return multiK;
                default:
                    return this;
            }
    }
}
}