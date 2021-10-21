using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Elly : SZYUCharacter {
    public override Color TextColor => new Color(1f, .84f, .9f);
    public override Color UIColor => new Color(0.4f, 0.1f, .24f);
    public override string Name => LocalizedStrings.FindReference("dialogue.elly");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class EllyMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Elly)};
}

}