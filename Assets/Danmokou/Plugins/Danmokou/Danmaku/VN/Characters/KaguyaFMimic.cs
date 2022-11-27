using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class KaguyaF : SZYUCharacter {
    public override Color TextColor => new(0.99f, 0.8f, .9f);
    public override Color UIColor => new(0.78f, 0.14f, 0.25f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.kaguyaf");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class KaguyaFMimic : SpriteCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(KaguyaF)};
}

}