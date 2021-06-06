using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class ShrineCourtyardBG : Rendered { }

public class ShrineCourtyardBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(ShrineCourtyardBG)};
}
}