﻿using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {
public class Kosuzu : SZYUCharacter {
    public override Color TextColor => new Color(1, .83f, .80f);
    public override Color UIColor => new Color(.72f, .38f, .19f);
    public override string Name => "Motoori Kosuzu";
    
    public override void RollEvent() => DependencyInjection.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KosuzuMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kosuzu)};
}

}