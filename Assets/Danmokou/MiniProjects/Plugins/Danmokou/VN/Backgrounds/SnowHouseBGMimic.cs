using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class SnowHouseBG : Rendered { }

public class SnowHouseBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(SnowHouseBG)};
}
}