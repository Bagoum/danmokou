using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class Key : Rendered { }

public class KeyMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(Key)};
}
}