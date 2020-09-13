using UnityEngine;

namespace Danmaku {
public class BossBEH : BehaviorEntity {
    public override bool TriggersUITimeout => true;

    public override bool OutOfHP() {
        ShiftPhase();
        return false;
    }
}
}