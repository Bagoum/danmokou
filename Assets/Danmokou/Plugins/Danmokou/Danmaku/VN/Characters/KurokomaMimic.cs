﻿using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Kurokoma : SZYUCharacter {
    public override Color TextColor => new(.9f, .74f, .67f);
    public override Color UIColor => new(0.65f, .19f, .17f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.kurokoma");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class KurokomaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kurokoma)};
}

}