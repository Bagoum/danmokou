using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class ForestBG : Rendered { }

public class ForestBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(ForestBG)};
}
}