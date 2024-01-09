using System;
using ProtoBuf;
using static Danmokou.Core.DInput.InputManager;

namespace Danmokou.Core.DInput {

/// <summary>
/// A struct storing all inputs required to replay one frame of a game.
/// <br/>The inner structure of what exactly is stored may differ between games. 
/// </summary>
[Serializable]
[ProtoContract]
public struct FrameInput {
    // 6-8 bytes (5 unpadded)
    // short(2)x2 = 4
    // byte(1)x1 = 1
    [ProtoMember(1)] public short horizontal;
    [ProtoMember(2)] public short vertical;
    [ProtoMember(3)] public byte data1;
    public FrameInput(short horiz, short vert, byte data1) {
        horizontal = horiz;
        vertical = vert;
        this.data1 = data1;
    }
}

}