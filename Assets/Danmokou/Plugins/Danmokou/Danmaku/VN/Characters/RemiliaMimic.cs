using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Remilia : SZYUCharacter {
    public static System.Numerics.Vector3 SpeakIconOffset => new(1.2f, 1.7f, 0);
    public override Color TextColor => new(.95f, .81f, .98f);
    public override Color UIColor => new(.52f, .05f, .36f);
    public override LString Name { get; set; } = LocalizedStrings.FindReference("dialogue.remilia");
    
    public override void RollEvent() => ServiceLocator.SFXService.Request("x-bubble-3", SFXType.TypingSound);
}

public class RemiliaMimic : PiecewiseCharacterMimic {
    public override Type[] CoreTypes => new[] {typeof(Remilia)};
}

}