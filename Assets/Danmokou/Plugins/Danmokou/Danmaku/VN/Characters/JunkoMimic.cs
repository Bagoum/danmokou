using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Junko : SZYUCharacter {
    public override Color TextColor => new(1, 1, 1);
    public override Color UIColor => new(0.6f, 0.2f, 0.11f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.junko");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class JunkoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Junko)};
}

}