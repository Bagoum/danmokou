using System;
using System.Threading;
using System.Threading.Tasks;
using SM;

namespace Danmaku {
/// <summary>
/// This is a singleton on the GameManagement object which can run cancellable coroutines independent of BEH.
/// </summary>
public class GlobalPauseUpdateBEH : BehaviorEntity {

    protected override void Awake() {
        base.Awake();
        CoroutineRegularUpdater.GlobalDuringPause = this;
    }

    public override bool UpdateDuringPause => true;

    public override int UpdatePriority => UpdatePriorities.SOF;

    public override bool OutOfHP() {
        throw new InvalidOperationException("Cannot call hp-death on global BEH");
    }
}
}