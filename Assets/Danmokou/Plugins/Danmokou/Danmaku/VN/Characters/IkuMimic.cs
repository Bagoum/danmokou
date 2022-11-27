using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Iku : SZYUCharacter {
    public override Color TextColor => new(1f, 0.92f, 0.96f);
    public override Color UIColor => new(.5f, 0f, .45f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.iku");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class IkuMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Iku)};
}

}