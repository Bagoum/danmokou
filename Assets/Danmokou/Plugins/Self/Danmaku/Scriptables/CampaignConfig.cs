using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject {
    public int startLives;
    public string key = "";
    public string shortTitle = "";
    public EndingConfig[] endings = null!;
    public PlayerConfig[] players = null!;
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