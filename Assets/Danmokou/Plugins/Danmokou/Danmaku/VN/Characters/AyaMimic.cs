﻿using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Aya : SZYUCharacter {
    public override Color TextColor => new(.9f, .9f, .9f);
    public override Color UIColor => new(.7f, .7f, .7f);
    public override string Name => LocalizedStrings.FindReference("dialogue.aya");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class AyaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Aya)};
}

}