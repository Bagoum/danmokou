using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class WaterfallBG : Rendered { }

public class WaterfallBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(WaterfallBG)};
}
}