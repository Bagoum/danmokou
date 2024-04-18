using System;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou {
[CreateAssetMenu(menuName = "Data/GameDef/EmptyCampaign")]
public class EmptyCampaignGameDef : CampaignDanmakuGameDef {
    public int Lives = 1;
    public override InstanceFeatures MakeFeatures(DifficultySettings difficulty, InstanceMode mode, long? highScore)
        => new() {
            Basic = new BasicFeatureCreator() {
                StartLives = Lives
            }
        };
}
}