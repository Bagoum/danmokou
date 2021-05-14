using Danmokou.Behavior;
using Danmokou.Core;

namespace Danmokou.Services {
/// <summary>
/// A globally available CRU, scoped per-scene. Instantiation handled by GameManagement.
/// </summary>
public class SceneLocalCRU : CoroutineRegularUpdater {
    public static SceneLocalCRU Main { get; private set; } = null!;
    protected void Awake() {
        Main = this;
    }
    public override int UpdatePriority => UpdatePriorities.SOF;
    
}
}