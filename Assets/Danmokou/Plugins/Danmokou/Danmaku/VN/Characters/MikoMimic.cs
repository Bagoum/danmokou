using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Miko : SZYUCharacter {
    public override Color TextColor => new Color(1, .83f, .79f);
    public override Color UIColor => new Color(0.4f, 0.01f, .24f);
    public override string Name => LocalizedStrings.FindReference("dialogue.miko");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class MikoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Miko)};
}

}