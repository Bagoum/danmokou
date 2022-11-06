using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Byakuren : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1.2f, 2.4f, 0);
    public override Color TextColor => new(1f, 0.9f, 0.8f);
    public override Color UIColor => new(0.70f, 0.24f, 0.70f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.byakuren");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class ByakurenMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Byakuren)};
}

}