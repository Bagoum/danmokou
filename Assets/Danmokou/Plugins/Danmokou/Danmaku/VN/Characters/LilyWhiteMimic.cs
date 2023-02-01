using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class LilyWhite : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1f, 1.7f, 0);
    public override Color TextColor => new(.98f, .88f, .92f);
    public override Color UIColor => new(.96f, .65f, .76f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.lilywhite");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-4", SFXType.TypingSound);
}

public class LilyWhiteMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(LilyWhite)};
}

}