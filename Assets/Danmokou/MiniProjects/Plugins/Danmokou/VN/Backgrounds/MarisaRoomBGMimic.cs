using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class MarisaRoomBG : Rendered { }

public class MarisaRoomBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(MarisaRoomBG)};
}
}