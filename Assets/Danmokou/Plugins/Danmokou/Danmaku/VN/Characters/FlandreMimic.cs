using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Flandre : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1.5f, 1.8f, 0);
    public override Color TextColor => new(1f, 0.92f, 0.76f);
    public override Color UIColor => new(.7f, .2f, .26f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.flandre");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class FlandreMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Flandre)};
}

}