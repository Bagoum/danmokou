using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Kutaka : SZYUCharacter {
    public override Color TextColor => new(.99f, .92f, .80f);
    public override Color UIColor => new(.80f, .53f, .25f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.kutaka");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KutakaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kutaka)};
}

}