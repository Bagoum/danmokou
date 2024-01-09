using Danmokou.Core;
using Danmokou.Core.DInput;
using static Danmokou.Core.DInput.InputManager;

namespace Danmokou.Services {

public class StandardDanmakuInputExtractor : IInputSource {
    public ReplayPlayer Input { get; }
    private FrameInput Frame => Input.CurrentFrame;

    public StandardDanmakuInputExtractor(ReplayPlayer input) {
        Input = input;
    }

    public static FrameInput RecordFrame => new(HorizontalSpeed, VerticalSpeed,
        BitCompression.FromBools(IsFiring, IsFocus, IsBomb, IsMeter, DialogueConfirm, IsSwap, DialogueSkipAll));
    
    short? IInputSource.HorizontalSpeed => Frame.horizontal;
    short? IInputSource.VerticalSpeed => Frame.vertical;
    bool? IInputSource.Firing => Frame.data1.NthBool(0);
    bool? IInputSource.Focus => Frame.data1.NthBool(1);
    bool? IInputSource.Bomb => Frame.data1.NthBool(2);
    bool? IInputSource.Meter => Frame.data1.NthBool(3);
    bool? IInputSource.DialogueConfirm => Frame.data1.NthBool(4);
    bool? IInputSource.Swap => Frame.data1.NthBool(5);
    bool? IInputSource.DialogueSkipAll => Frame.data1.NthBool(6);

    //handled by replay player's step function
    public bool OncePerUnityFrameToggleControls() => false;
}
}