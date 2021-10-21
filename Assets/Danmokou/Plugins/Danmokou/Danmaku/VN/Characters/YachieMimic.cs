using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Yachie : SZYUCharacter {
    public override Color TextColor => new Color(0.97f, 1, .89f);
    public override Color UIColor => new Color(0.07f, 0.63f, 0.34f);
    public override string Name => LocalizedStrings.FindReference("dialogue.yachie");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class YachieMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yachie)};
}

}