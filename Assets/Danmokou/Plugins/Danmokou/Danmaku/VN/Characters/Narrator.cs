using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Narrator : SZYUCharacter {
    public override bool MimicRequested => false;
    public override Color TextColor => new Color(0.94f, 0.94f, 0.94f);
    public override Color UIColor => new Color(0.78f, 0.14f, 0.25f);
    public override string Name => LocalizedStrings.FindReference("dialogue.narrator");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

}