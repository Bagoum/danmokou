using DMK.Behavior;
using DMK.Core;

namespace DMK.Services {
/// <summary>
/// A globally available CRU, scoped per-scene. Instantiation handled by GameManagement.
/// </summary>
public class SceneLocalCRU : CoroutineRegularUpdater {
    public static SceneLocalCRU Main { get; private set; }
    protected void Awake() {
        Main = this;
    }
    public override int UpdatePriority => UpdatePriorities.SOF;
    
}
}