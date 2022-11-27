using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Kasen : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1.2f, 2.4f, 0);
    public override Color TextColor => new(1, 0.82f, 0.88f);
    public override Color UIColor => new(0.22f, 0.59f, 0.11f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.kasen");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class KasenMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kasen)};
}

}