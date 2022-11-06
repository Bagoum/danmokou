using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Cirno : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 2, 0);
    public override Color TextColor => new(0.83f, 0.96f, 1f);
    public override Color UIColor => new(.2f, 0.6f, .7f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.cirno");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class CirnoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Cirno)};
}

}