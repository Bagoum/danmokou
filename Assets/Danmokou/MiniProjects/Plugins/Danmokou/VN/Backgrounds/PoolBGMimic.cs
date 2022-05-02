using System;
using System.Collections;
using System.Collections.Generic;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {

public class PoolBG : Rendered { }

public class PoolBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(PoolBG)};
}
}