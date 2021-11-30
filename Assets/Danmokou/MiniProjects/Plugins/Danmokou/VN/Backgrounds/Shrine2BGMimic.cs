using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Shrine2BG : Rendered { }

public class Shrine2BGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Shrine2BG)};
}
}