using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class SnowHillBG : Rendered { }

public class SnowHillBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(SnowHillBG)};
}
}