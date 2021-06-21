using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject {
    public int startLives;
    public string key = "";
    public string shortTitle = "";
    public bool replayable = true;
    public EndingConfig[] endings = null!;
    public ShipConfig[] players = null!;
    public StageConfig[] stages = null!;
    public BossConfig[] practiceBosses = null!;

    public bool TryGetEnding(out EndingConfig ed) {
        ed = default!;
        foreach (var e in endings) {
            if (e.Matches) {
                ed = e;
                return true;
            }
        }
        return false;
    }
}
}