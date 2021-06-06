using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class TownBG : Rendered { }

public class TownBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(TownBG)};
}
}