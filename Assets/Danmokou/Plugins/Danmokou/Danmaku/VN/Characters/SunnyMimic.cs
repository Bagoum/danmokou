using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Sunny : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1f, 1.7f, 0);
    public override Color TextColor => new(.95f, .75f, .65f);
    public override Color UIColor => new(.68f, .33f, .18f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.sunny");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class SunnyMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Sunny)};
}

}