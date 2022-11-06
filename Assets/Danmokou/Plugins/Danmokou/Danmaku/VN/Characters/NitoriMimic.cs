using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Nitori : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(0.9f, 1.9f, 0);
    public override Color TextColor => new(.85f, 0.94f, 1f);
    public override Color UIColor => new(.15f, .3f, .6f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.nitori");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class NitoriMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Nitori)};
}

}