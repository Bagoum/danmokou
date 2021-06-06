using System;
using System.Collections;
using System.Collections.Generic;
using Suzunoya.Entities;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {

public class LibraryBG : Rendered { }

public class LibraryBGMimic : OneSpriteMimic {
    public override Type[] CoreTypes => new[] {typeof(LibraryBG)};
}
}