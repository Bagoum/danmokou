using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Book2 : Rendered { }

public class Book2Mimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Book2)};
}
}