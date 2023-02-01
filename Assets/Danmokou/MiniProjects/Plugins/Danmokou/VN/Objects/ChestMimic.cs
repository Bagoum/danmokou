using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Chest : Rendered { }

public class ChestMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Chest)};
}
}