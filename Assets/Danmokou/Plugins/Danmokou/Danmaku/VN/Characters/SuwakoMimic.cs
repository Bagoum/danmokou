using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Suwako : SZYUCharacter {
    public override Color TextColor => new(1, .83f, .79f);
    public override Color UIColor => new(0.9f, 0.9f, .5f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.suwako");

    public string DialogueSFX { get; set; } = "x-bubble-4";
    
    public override void RollEvent() => ServiceLocator.SFXService.Request(DialogueSFX, SFXType.TypingSound);
}

public class SuwakoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Suwako)};
}

}