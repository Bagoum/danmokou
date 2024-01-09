using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Eirin : SZYUCharacter {
    public override Color TextColor => new(.98f, .7f, .77f);
    public override Color UIColor => new(.2f, .17f, .53f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.eirin");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class EirinMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Eirin)};
}

}