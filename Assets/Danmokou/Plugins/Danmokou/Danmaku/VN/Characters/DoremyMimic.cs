using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Doremy : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(0.8f, 2.3f, 0);
    public override Color TextColor => new(0.98f, 0.85f, 1f);
    public override Color UIColor => new(.5f, 0.1f, .65f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.doremy");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class DoremyMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Doremy)};
}

}