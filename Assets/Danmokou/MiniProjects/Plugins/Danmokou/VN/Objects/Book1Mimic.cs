using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Book1 : Rendered { }

public class Book1Mimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Book1)};
}
}