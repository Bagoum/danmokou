using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Narrator : SZYUCharacter {
    public override bool MimicRequested => false;
    public override Color TextColor => new(0.94f, 0.94f, 0.94f);
    public override Color UIColor => new(0.78f, 0.14f, 0.25f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.narrator");
    
    public override void RollEvent() => ISFXService.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}
public class SilentNarrator : SZYUCharacter {
    public override bool MimicRequested => false;
    public override Color TextColor => new(0.94f, 0.94f, 0.94f);
    public override Color UIColor => new(0.5f, 0.5f, 0.5f);
    public override LString Name { get; set; } = "";
}

}