using UnityEngine;

namespace Danmokou.Core {
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

[System.Serializable]
public struct SMAsset {
    public string name;
    public TextAsset file;
}

[System.Serializable]
public struct SMAssetGroup {
    public string groupTitle;
    public SMAsset[] assets;
}

}