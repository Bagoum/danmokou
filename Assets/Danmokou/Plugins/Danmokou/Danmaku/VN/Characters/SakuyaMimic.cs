using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Sakuya : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 2.4f, 0);
    public override Color TextColor => new(.88f, .92f, .96f);
    public override Color UIColor => new(.56f, .66f, .76f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.sakuya");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class SakuyaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Sakuya)};
}

}