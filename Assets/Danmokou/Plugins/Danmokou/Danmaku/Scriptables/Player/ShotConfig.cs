using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
/// <summary>
/// Provides shot metadata.
/// </summary>
[CreateAssetMenu(menuName = "Data/Player/Shot")]
public class ShotConfig : ScriptableObject {
    public enum StarRating {
        Zero,
        One,
        Two,
        Three,
        Five
    }
    
    public string key = "";
    /// <summary>
    /// eg. "Homing Needles - Persuasion Laser"
    /// </summary>
    public LocalizedStringReference title = null!;
    public LString Title => title.Value;
    [TextArea(5, 10)] public string description = "";
    /// <summary>
    /// eg. "Forward Focus"
    /// </summary>
    public string type = "";
    [Header("Unitary Shot Configuration")] public GameObject prefab = null!;
    public bool playerChild = true;
    public StaticReplay? demoReplay;
    public TextAsset? demoSetupSM;
    public StarRating shotDifficulty;
    [Header("Multi-Shot Configuration")] public bool isMultiShot;
    public ShotConfig? multiD;
    public ShotConfig? multiM;
    public ShotConfig? multiK;
    public SFXConfig? onSwap;
    public IEnumerable<ShotConfig>? Subshots => isMultiShot ?
        new[] {multiD!, multiM!, multiK!} :
        null;

    /// <summary>
    /// In the format Mokou-A
    /// </summary>
    public static string PlayerShotDescription(ShipConfig? p, ShotConfig? s) {
        var playerDesc = (p == null) ? "???" : (string)p.ShortTitle;
        var shotDesc = "?";
        if (p != null && s != null) {
            var os = p.shots2.FirstOrDefault(_os => _os.shot == s);
            if (os.shot == s) {
                if (string.IsNullOrWhiteSpace(os.ordinal)) {
                    if (os.shot.isMultiShot) shotDesc = "X";
                } else shotDesc = os.ordinal;
            }
        }
        return $"{playerDesc}-{shotDesc}";
    }

    public ShotConfig GetSubshot(Subshot sub) {
        if (!isMultiShot) return this;
        else
            return sub switch {
                Subshot.TYPE_D => multiD!,
                Subshot.TYPE_M => multiM!,
                Subshot.TYPE_K => multiK!,
                _ => this
            };
    }
}
}