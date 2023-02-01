using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Tokiko : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 1.7f, 0);
    public override Color TextColor => new(0.94f, 0.94f, 1f);
    public override Color UIColor => new(0.1f, 0.1f, 0.34f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.tokiko");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class TokikoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Tokiko)};
}

}