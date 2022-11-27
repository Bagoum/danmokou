using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Komakusa : SZYUCharacter {
    public override Color TextColor => new(1, .85f, .90f);
    public override Color UIColor => new(0.6f, .07f, .59f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.komakusa");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KomakusaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Komakusa)};
}

}