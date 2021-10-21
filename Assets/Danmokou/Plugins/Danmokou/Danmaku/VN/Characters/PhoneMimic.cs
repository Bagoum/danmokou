using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Phone : SZYUCharacter {
    public override Color TextColor => new Color(0.94f, 0.94f, 0.94f);
    public override Color UIColor => new Color(0.78f, 0.14f, 0.25f);
    public override string Name => LocalizedStrings.FindReference("dialogue.phone");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class PhoneMimic : SpriteCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Phone)};
}

}