using System;
using Danmokou.Core;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {
public class Yukari : SZYUCharacter {
    public override Color TextColor => new Color(0.94f, 0.89f, 1f);
    public override Color UIColor => new Color(0.70f, 0.14f, 0.77f);
    public override string Name => "Yakumo Yukari";
    
    public override void RollEvent() => DependencyInjection.SFXService.Request("x-bubble-3");
}

public class YukariMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Yukari)};
}

}