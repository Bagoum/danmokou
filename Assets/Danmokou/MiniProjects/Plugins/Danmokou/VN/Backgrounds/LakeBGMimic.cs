using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class LakeBG : Rendered { }

public class LakeBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(LakeBG)};
}
}