using UnityEngine;

//Inspector-exposed structs cannot be readonly
[System.Serializable]
public struct DataPrefab {
    public string name;
    public GameObject prefab;
}

[System.Serializable]
public struct PrefabGroup {
    public string groupTitle;
    public DataPrefab[] prefabs;

}

[CreateAssetMenu(menuName = "Data/PrefabList")]
public class SOPrefabs : ScriptableObject {
    public PrefabGroup[] prefabs;
}
