using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class NormalBedroomBG : Rendered { }

public class NormalBedroomBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(NormalBedroomBG)};
}
}