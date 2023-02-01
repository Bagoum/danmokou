using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Star : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1f, 1.5f, 0);
    public override Color TextColor => new(.75f, .81f, .95f);
    public override Color UIColor => new(.22f, .34f, .63f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.star");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class StarMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Star)};
}

}