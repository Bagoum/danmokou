using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Mokou : SZYUCharacter {
    public override Color TextColor => new(.97f, .75f, .864f);
    public override Color UIColor => new(.63f, .03f, .14f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.mokou");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MokouMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Mokou)};
}

}