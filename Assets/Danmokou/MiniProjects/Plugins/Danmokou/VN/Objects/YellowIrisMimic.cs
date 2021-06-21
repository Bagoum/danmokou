using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class YellowIris : Rendered { }

public class YellowIrisMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(YellowIris)};
}
}