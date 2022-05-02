using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Eraa : SZYUCharacter {
    public override Color TextColor => new(1f, 0.82f, 0.6f);
    public override Color UIColor => new(0.22f, 0.59f, 0.11f);
    public override LString Name { get; set; } = "<color=red>[Null Reference Exception]</color>";
    
    public override void RollEvent() { }
}

public class EraaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Eraa)};
}

}