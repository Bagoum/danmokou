using System.Linq;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using UnityEngine;

namespace MiniProjects.VN {

[CreateAssetMenu(menuName = "Data/GameDef/FlappyBird")]
public class FBGameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings d, InstanceMode m, long? highScore) => new() {
        Configuration = new ConfigurationFeatureCreator() { PoCLocation = 4, TraditionalRespawn = true },
        Score = new ScoreFeatureCreator(highScore),
        Power = new PowerFeatureCreator(),
        Faith = new FaithFeatureCreator(),
        ItemExt = new LifeItemExtendFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        ScoreExt = new ScoreExtendFeatureCreator(),
        Meter = new DisabledMeterFeatureCreator()
    };

    public override (RebindableInputBinding[] kbm, RebindableInputBinding[] controller) GetRebindableControls() =>
        (InputSettings.i.KBMBindings, InputSettings.i.ControllerBindings.Take(1).ToArray());
}
}