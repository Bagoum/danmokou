using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Yuyuko : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1.0f, 2.6f, 0);
    public override Color TextColor => new(1f, 0.90f, 0.93f);
    public override Color UIColor => new(0.90f, 0.54f, 0.70f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.yuyuko");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class YuyukoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yuyuko)};
}

}