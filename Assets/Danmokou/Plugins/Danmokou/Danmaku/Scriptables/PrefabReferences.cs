using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Prefab References")]
public class PrefabReferences : ScriptableObject {
    public GameObject inode = null!;
    public GameObject arbitraryCapturer = null!;
    public GameObject cutinGhost = null!;

    public GameObject phasePerformance = null!;
}
}
