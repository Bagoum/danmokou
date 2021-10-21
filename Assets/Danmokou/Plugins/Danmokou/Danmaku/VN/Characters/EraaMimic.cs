using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Eraa : SZYUCharacter {
    public override Color TextColor => new Color(1f, 0.82f, 0.6f);
    public override Color UIColor => new Color(0.22f, 0.59f, 0.11f);
    public override string Name => LocalizedStrings.FindReference("dialogue.eraa");
    
    public override void RollEvent() { }
}

public class EraaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Eraa)};
}

}