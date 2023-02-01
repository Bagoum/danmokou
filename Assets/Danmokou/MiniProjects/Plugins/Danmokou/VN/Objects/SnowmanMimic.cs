using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Snowman : Rendered { }

public class SnowmanMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Snowman)};
}
}