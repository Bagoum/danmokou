using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {
public class Komakusa : SZYUCharacter {
    public override Color TextColor => new Color(1, .85f, .90f);
    public override Color UIColor => new Color(0.6f, .07f, .59f);
    public override string Name => "Komakusa Sannyo";
    
    public override void RollEvent() => DependencyInjection.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KomakusaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Komakusa)};
}

}