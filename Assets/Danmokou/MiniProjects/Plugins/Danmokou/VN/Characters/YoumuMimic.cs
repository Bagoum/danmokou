using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace MiniProjects.VN {
public class Youmu : SZYUCharacter {
    public override Color TextColor => new Color(.76f, .93f, .80f);
    public override Color UIColor => new Color(1, 1, 1);
    public override string Name => "Konpaku Youmu";
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class YoumuMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Youmu)};
}

}