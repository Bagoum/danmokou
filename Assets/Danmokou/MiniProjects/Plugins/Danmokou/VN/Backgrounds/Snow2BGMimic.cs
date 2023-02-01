using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Snow2BG : Rendered { }

public class Snow2BGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Snow2BG)};
}
}