using Danmokou.Core;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/PrefabList")]
public class SOPrefabs : ScriptableObject {
    public PrefabGroup[] prefabs = null!;
}
}