using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Shinki : SZYUCharacter {
    public override Color TextColor => new(0.92f, 0.78f, 0.95f);
    public override Color UIColor => new(.6f, .4f, .3f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.shinki");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class ShinkiMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Shinki)};
}

}