using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Koishi : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1, 2, 0);
    public override Color TextColor => new(1f, 0.85f, 0.92f);
    public override Color UIColor => new(0.85f, 0.1f, 0.24f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.koishi");

    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KoishiMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Koishi)};
}

}