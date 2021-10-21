using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Yukari : SZYUCharacter {
    public override Color TextColor => new Color(0.94f, 0.89f, 1f);
    public override Color UIColor => new Color(0.70f, 0.14f, 0.77f);
    public override string Name => LocalizedStrings.FindReference("dialogue.yukari");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class YukariMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yukari)};
}

}