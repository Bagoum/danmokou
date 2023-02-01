using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Objection : Rendered { }

public class ObjectionMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Objection)};
}
}