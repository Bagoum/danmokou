using Danmokou;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using UnityEngine;

namespace MiniProjects {
[CreateAssetMenu(menuName = "Data/GameDef/BHA3")]
public class BHA3GameDef : CampaignDanmakuGameDef {
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