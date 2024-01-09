using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Core.DInput.InputManager;

namespace MiniProjects.VN {

[CreateAssetMenu(menuName = "Data/GameDef/FlappyBird")]
public class FBGameDef : CampaignDanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings d, InstanceMode m, long? highScore) => new() {
        Basic = new BasicFeatureCreator() { Continues = 0, StartLives = m.OneLife() ? 1 : Campaign.StartLives },
        Configuration = new ConfigurationFeatureCreator() { PoCLocation = 3f, TraditionalRespawn = true },
        Score = new ScoreFeatureCreator(highScore) { AllowPointPlusItems = false },
        Power = new DisabledPowerFeatureCreator(),
        Faith = new FaithFeatureCreator(),
        ItemExt = new LifeItemExtendFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        ScoreExt = new ScoreExtendFeatureCreator(),
        Meter = new DisabledMeterFeatureCreator()
    };
    
    public override (IEnumerable<RebindableInputBinding> kbm, IEnumerable<RebindableInputBinding> controller) GetRebindableControls() =>
        (InputSettings.i.KBMBindings.Except(new[]{InputSettings.i.FocusHold, InputSettings.i.Special, InputSettings.i.Swap}), 
            InputSettings.i.ControllerBindings.Except(new[]{InputSettings.i.CFocusHold, InputSettings.i.CSpecial, InputSettings.i.CSwap}));
    
    
    public override FrameInput RecordReplayFrame() => FlappyDanmakuInputExtractor.RecordFrame;
    public override IInputSource CreateReplayInputSource(ReplayPlayer input) =>
        new FlappyDanmakuInputExtractor(input);
}

public class FlappyDanmakuInputExtractor : IInputSource {
    public ReplayPlayer Input { get; }
    private FrameInput Frame => Input.CurrentFrame;

    public FlappyDanmakuInputExtractor(ReplayPlayer input) {
        Input = input;
    }

    public static FrameInput RecordFrame => new(HorizontalSpeed, VerticalSpeed,
        BitCompression.FromBools(IsFiring, IsFly, IsSlowFall, DialogueConfirm, DialogueSkipAll));
    
    short? IInputSource.HorizontalSpeed => Frame.horizontal;
    short? IInputSource.VerticalSpeed => Frame.vertical;
    bool? IInputSource.Firing => Frame.data1.NthBool(0);
    bool? IInputSource.Fly => Frame.data1.NthBool(1);
    bool? IInputSource.SlowFall => Frame.data1.NthBool(2);
    bool? IInputSource.DialogueConfirm => Frame.data1.NthBool(3);
    bool? IInputSource.DialogueSkipAll => Frame.data1.NthBool(4);

    //handled by replay player's step function
    public bool OncePerUnityFrameToggleControls() => false;
}

}