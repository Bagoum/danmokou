using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class ScreenColor : Rendered { }

public class ScreenColorMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(ScreenColor)};
}
}