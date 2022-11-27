using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Murasa : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(0.7f, 2.4f, 0);
    public override Color TextColor => new(.92f, .97f, .92f);
    public override Color UIColor => new(.75f, .92f, .77f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.murasa");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MurasaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Murasa)};
}

}