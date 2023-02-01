using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Snow1BG : Rendered { }

public class Snow1BGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Snow1BG)};
}
}