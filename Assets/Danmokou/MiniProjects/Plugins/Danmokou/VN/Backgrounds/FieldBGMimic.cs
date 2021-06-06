using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class FieldBG : Rendered { }

public class FieldBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(FieldBG)};
}
}