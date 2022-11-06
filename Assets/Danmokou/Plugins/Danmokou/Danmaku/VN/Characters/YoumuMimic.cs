using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Youmu : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 2, 0);
    public override Color TextColor => new(.8f, .99f, .85f);
    public override Color UIColor => new(1, 1, 1);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.youmu");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class YoumuMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Youmu)};
}

}