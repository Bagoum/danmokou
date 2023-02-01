using System;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou {
[CreateAssetMenu(menuName = "Data/GameDef/Empty")]
public class EmptyGameDef : DanmakuGameDef {
    public override InstanceFeatures MakeFeatures(DifficultySettings _, InstanceMode m, long? __) => InstanceFeatures.InactiveFeatures;
    public override IEnumerable<ShipConfig> AllShips => Array.Empty<ShipConfig>();
}
}