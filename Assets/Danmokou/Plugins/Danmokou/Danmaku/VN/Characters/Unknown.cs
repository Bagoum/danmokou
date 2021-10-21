using System;
using Danmokou.Core;
using Danmokou.Services;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Mimics;
using UnityEngine;

namespace Danmokou.VN {
public class Unknown : SZYUCharacter {
    public override bool MimicRequested => false;
    public override Color TextColor => new Color(0.87f, 0.87f, 0.87f);
    public override Color UIColor => new Color(0.5f, 0.5f, 0.5f);
    public override string Name => LocalizedStrings.FindReference("dialogue.unknown");
    
    public override void RollEvent() {}
}

}