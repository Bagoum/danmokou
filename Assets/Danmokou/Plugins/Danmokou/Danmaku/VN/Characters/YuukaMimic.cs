using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Yuuka : SZYUCharacter {
    public override Color TextColor => new Color(0.84f, 0.99f, .82f);
    public override Color UIColor => new Color(0.78f, 0.14f, 0.25f);
    public override string Name => LocalizedStrings.FindReference("dialogue.yuuka");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class YuukaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yuuka)};
}

}