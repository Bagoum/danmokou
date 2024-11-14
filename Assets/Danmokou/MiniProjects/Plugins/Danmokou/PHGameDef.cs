using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using UnityEngine;

namespace MiniProjects {
[CreateAssetMenu(menuName = "Data/GameDef/PlasticHakkero")]
public class PHGameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings d, InstanceMode mode, long? highScore) => new() {
        Basic = new BasicFeatureCreator {
            Continues = 0, 
            StartLives = mode.OneLife() ? 1 : Campaign.StartLives
        },
        Score = new ScoreFeatureCreator(highScore) { AllowPointPlusItems = false },
        Power = new DisabledPowerFeatureCreator(),
        Faith = new DisabledFaithFeatureCreator(),
        ItemExt = new DisabledLifeItemExtendFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        ScoreExt = new DisabledScoreExtendFeatureCreator(),
        Meter = new MeterFeatureCreator() {
            MeterUseThreshold = 0.12f
        },
    };
}
}