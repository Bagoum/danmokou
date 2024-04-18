using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using UnityEngine;

namespace MiniProjects {
[CreateAssetMenu(menuName = "Data/GameDef/THJam13")]
public class THJam13GameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings d, InstanceMode m, long? highScore) => new() {
        Basic = new BasicFeatureCreator {
            Continues = m.OneLife() ? 0 : 42, 
            StartLives = m.OneLife() ? 1 : Campaign.StartLives
        },
        Score = new ScoreFeatureCreator(highScore),
        Power = new DisabledPowerFeatureCreator(),
        Faith = new FaithFeatureCreator(),
        ItemExt = new LifeItemExtendFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        ScoreExt = new ScoreExtendFeatureCreator(),
        Meter = d.meterEnabled ? 
            new MeterFeatureCreator() : 
            new DisabledMeterFeatureCreator(),
        CustomData = new THJam13CustomDataFeatureCreator()
    };
    
}
}