﻿using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Seija : SZYUCharacter {
    public override Color TextColor => new(0.86f, .7f, .96f);
    public override Color UIColor => new(0.32f, 0.01f, .4f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.seija");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class SeijaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Seija)};
}

}