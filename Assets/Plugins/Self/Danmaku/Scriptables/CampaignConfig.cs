using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject {
    public string key;
    public string shortTitle;
    public StageConfig[] stages;

    public BossConfig[] practiceBosses;
}