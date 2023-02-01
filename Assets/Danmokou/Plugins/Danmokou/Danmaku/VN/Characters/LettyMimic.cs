using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Letty : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(0.6f, 1.8f, 0);
    public override Color TextColor => new(.96f, .85f, 1);
    public override Color UIColor => new(.89f, .67f, .95f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.letty");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class LettyMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Letty)};
}

}