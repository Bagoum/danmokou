using System;
using System.Threading;
using System.Threading.Tasks;
using SM;

namespace Danmaku {
/// <summary>
/// This is a singleton on the GameManagement object which can run cancellable coroutines independent of BEH.
/// </summary>
public class GlobalBEH : BehaviorEntity {
    public static GlobalBEH Main { get; private set; }

    protected override void Awake() {
        base.Awake();
        Main = this;
        CoroutineRegularUpdater.Global = this;
    }
    public override int UpdatePriority => UpdatePriorities.SOF;
}
}