using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class HakugyokurouBG : Rendered { }

public class HakugyokurouBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(HakugyokurouBG)};
}
}