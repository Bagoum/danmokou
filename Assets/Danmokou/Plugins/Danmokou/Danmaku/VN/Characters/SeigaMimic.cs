using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Seiga : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(0.8f, 2.5f, 0);
    public override Color TextColor => new(.8f, 1f, 1f);
    public override Color UIColor => new(.3f, .6f, .6f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.seiga");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class SeigaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Seiga)};
}

}