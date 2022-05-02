﻿using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Marisa : SZYUCharacter {
    public override Color TextColor => new(.96f, .92f, .79f);
    public override Color UIColor => new(1, .77f, .26f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.marisa");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MarisaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Marisa)};
}

}