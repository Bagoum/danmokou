using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Junko : SZYUCharacter {
    public override Color TextColor => new Color(1, 1, 1);
    public override Color UIColor => new Color(0.6f, 0.2f, 0.11f);
    public override string Name => LocalizedStrings.FindReference("dialogue.junko");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class JunkoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Junko)};
}

}