using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Kaguya : SZYUCharacter {
    public override Color TextColor => new(1f, .86f, .86f);
    public override Color UIColor => new(.76f, .18f, .45f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.kaguya");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KaguyaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kaguya)};
}

}