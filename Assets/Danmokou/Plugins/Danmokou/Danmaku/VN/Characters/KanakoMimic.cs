using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Kanako : SZYUCharacter {
    public override Color TextColor => new(0.92f, 0.85f, 1f);
    public override Color UIColor => new(0.34f, 0.1f, 0.85f);
    public override string Name => LocalizedStrings.FindReference("dialogue.kanako");

    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class KanakoMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kanako)};
}

}