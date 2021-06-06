using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class FarmBG : Rendered { }

public class FarmBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(FarmBG)};
}
}