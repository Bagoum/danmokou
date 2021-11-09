using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Mayumi : SZYUCharacter {
    public override Color TextColor => new(.99f, .84f, .81f);
    public override Color UIColor => new(.90f, .69f, .22f);
    public override string Name => LocalizedStrings.FindReference("dialogue.mayumi");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-2", SFXType.TypingSound);
}

public class MayumiMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Mayumi)};
}

}