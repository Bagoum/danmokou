using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Sanae : SZYUCharacter {
    public override Color TextColor => new(0.85f, 1f, 0.92f);
    public override Color UIColor => new(0.1f, 0.85f, 0.34f);
    public override string Name => LocalizedStrings.FindReference("dialogue.sanae");

    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class SanaeMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Sanae)};
}

}