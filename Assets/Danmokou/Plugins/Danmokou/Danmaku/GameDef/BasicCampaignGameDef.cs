using System;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou {
[CreateAssetMenu(menuName = "Data/GameDef/BasicCampaign")]
public class BasicCampaignGameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings difficulty, InstanceMode mode, long? highScore)
        => new() {
            Basic = new BasicFeatureCreator() {
                StartLives = (Campaign == null) ? 999999 : Campaign.StartLives
            }
        };
}
}