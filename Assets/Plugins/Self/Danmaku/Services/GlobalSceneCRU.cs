using System;
using System.Threading;
using System.Threading.Tasks;
using SM;

namespace Danmaku {
/// <summary>
/// A globally available CRU, scoped per-scene, located on the UIManager.
/// </summary>
public class GlobalSceneCRU : CoroutineRegularUpdater {
    public static GlobalSceneCRU Main { get; private set; }
    protected void Awake() {
        Main = this;
    }
    public override int UpdatePriority => UpdatePriorities.SOF;
    
}
}