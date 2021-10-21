using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Sagume : SZYUCharacter {
    public override Color TextColor => new Color(1f, 0.87f, 1f);
    public override Color UIColor => new Color(0.4f, 0.1f, 0.34f);
    public override string Name => LocalizedStrings.FindReference("dialogue.sagume");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class SagumeMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Sagume)};
}

}