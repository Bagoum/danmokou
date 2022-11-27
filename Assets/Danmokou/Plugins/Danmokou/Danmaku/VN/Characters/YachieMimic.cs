using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Yachie : SZYUCharacter {
    public override Color TextColor => new(0.97f, 1, .89f);
    public override Color UIColor => new(0.07f, 0.63f, 0.34f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.yachie");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class YachieMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yachie)};
}

}