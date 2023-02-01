using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Luna : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1f, 1.7f, 0);
    public override Color TextColor => new(1f, .99f, .93f);
    public override Color UIColor => new(.59f, .53f, .26f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.luna");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class LunaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Luna)};
}

}