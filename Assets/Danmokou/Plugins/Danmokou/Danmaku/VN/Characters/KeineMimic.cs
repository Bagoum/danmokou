﻿using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Keine : SZYUCharacter {
    public override Color TextColor => new(.8f, 1f, 1f);
    public override Color UIColor => new(.3f, .6f, .6f);
    public override string Name => LocalizedStrings.FindReference("dialogue.keine");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class KeineMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Keine)};
}

}