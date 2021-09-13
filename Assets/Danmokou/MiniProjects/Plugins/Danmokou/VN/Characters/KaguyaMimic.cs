using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {
public class Kaguya : SZYUCharacter {
    public override Color TextColor => new Color(1f, .86f, .86f);
    public override Color UIColor => new Color(.76f, .18f, .45f);
    public override string Name => "Houraisan Kaguya";
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class KaguyaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Kaguya)};
}

}