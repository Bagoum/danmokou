using Danmokou;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using UnityEngine;

namespace Danmokou.Miniprojects {
[CreateAssetMenu(menuName = "Data/GameDef/JY")]
public class JYGameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings d, InstanceMode m, long? highScore) => new() {
        Score = new ScoreFeatureCreator(highScore),
        Power = new DisabledPowerFeatureCreator(),
        Faith = new FaithFeatureCreator(),
        ItemExt = new LifeItemExtendFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        ScoreExt = new ScoreExtendFeatureCreator(),
        Meter = d.meterEnabled ? 
            new MeterFeatureCreator() : 
            new DisabledMeterFeatureCreator()
    };
}
}