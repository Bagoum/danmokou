using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Shinki : SZYUCharacter {
    public override Color TextColor => new Color(0.92f, 0.78f, 0.95f);
    public override Color UIColor => new Color(.6f, .4f, .3f);
    public override string Name => LocalizedStrings.FindReference("dialogue.shinki");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class ShinkiMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Shinki)};
}

}