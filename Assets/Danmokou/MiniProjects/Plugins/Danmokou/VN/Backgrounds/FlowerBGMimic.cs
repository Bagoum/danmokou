using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class FlowerBG : Rendered { }

public class FlowerBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(FlowerBG)};
}
}