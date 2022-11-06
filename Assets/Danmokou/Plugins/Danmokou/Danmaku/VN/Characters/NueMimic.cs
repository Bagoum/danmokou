using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Nue : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 2.1f, 0);
    public override Color TextColor => new(.94f, .87f, .88f);
    public override Color UIColor => new(.34f, .38f, .45f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.nue");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class NueMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Nue)};
}

}