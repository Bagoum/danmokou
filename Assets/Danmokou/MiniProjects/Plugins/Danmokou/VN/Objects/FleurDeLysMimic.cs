using System;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;

namespace MiniProjects.VN {
public class FleurDeLys : Rendered { }

public class FleurDeLysMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(FleurDeLys)};
}
}