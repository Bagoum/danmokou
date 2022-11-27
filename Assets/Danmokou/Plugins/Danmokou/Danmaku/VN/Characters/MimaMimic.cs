using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Mima : SZYUCharacter {
    public override Color TextColor => new(.75f, 1f, 0.83f);
    public override Color UIColor => new(.2f, .5f, .7f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.mima");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MimaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Mima)};
}

}