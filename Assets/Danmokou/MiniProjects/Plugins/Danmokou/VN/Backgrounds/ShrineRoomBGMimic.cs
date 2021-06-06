using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class ShrineRoomBG : Rendered { }

public class ShrineRoomBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(ShrineRoomBG)};
}
}