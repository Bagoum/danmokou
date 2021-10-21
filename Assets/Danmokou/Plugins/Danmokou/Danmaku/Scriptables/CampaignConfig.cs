using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
public interface ICampaignMeta {
    string Key { get; }
    bool Replayable { get; }
    bool AllowDialogueSkip { get; }
}

[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject, ICampaignMeta {
    public int startLives;
    public string key = "";
    public string shortTitle = "";
    public bool replayable = true;
    public bool allowDialogueSkip = true;
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

    public string Key => key;
    public bool Replayable => replayable;
    public bool AllowDialogueSkip => allowDialogueSkip;
}
}