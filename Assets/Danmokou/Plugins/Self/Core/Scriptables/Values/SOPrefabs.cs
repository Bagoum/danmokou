using DMK.Core;
using UnityEngine;


namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/PrefabList")]
public class SOPrefabs : ScriptableObject {
    public PrefabGroup[] prefabs;
}
}