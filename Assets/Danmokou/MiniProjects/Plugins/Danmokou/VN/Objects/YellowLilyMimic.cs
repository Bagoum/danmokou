using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class YellowLily : Rendered { }

public class YellowLilyMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(YellowLily)};
}
}