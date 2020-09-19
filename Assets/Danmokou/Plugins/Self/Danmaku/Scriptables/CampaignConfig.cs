using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject {
    public int startLives;
    public string key;
    public string shortTitle;
    public EndingConfig[] endings;
    public PlayerConfig[] players;
    public StageConfig[] stages;
    public BossConfig[] practiceBosses;

    public bool TryGetEnding(out EndingConfig ed) {
        ed = default;
        foreach (var e in endings) {
            if (e.Matches) {
                ed = e;
                return true;
            }
        }
        return false;
    }
}