using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Mima : SZYUCharacter {
    public override Color TextColor => new Color(.75f, 1f, 0.83f);
    public override Color UIColor => new Color(.2f, .5f, .7f);
    public override string Name => LocalizedStrings.FindReference("dialogue.mima");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MimaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Mima)};
}

}