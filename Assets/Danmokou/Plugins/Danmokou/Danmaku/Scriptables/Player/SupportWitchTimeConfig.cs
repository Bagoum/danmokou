using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Player;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {

[CreateAssetMenu(menuName = "Data/Player/Support/WitchTime")]
public class SupportWitchTimeConfig : SupportAbilityConfig {
    public override string Key => "WitchTime";
    public override SupportAbility Value => new WitchTime() {
        title = title.Value,
        shortTitle = shortTitle.Value
    };
}
}