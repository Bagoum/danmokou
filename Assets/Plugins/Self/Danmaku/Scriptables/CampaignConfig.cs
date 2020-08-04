using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : ScriptableObject {
    public string description;
    public StageConfig[] stages;

    public BossConfig[] practiceBosses;
    public StageConfig[] practiceStages;
}