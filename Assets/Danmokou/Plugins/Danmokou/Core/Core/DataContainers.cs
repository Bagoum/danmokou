﻿using UnityEngine;

namespace Danmokou.Core {
//Inspector-exposed structs cannot be readonly
[System.Serializable]
public struct DataPrefab {
    public string name;
    public GameObject prefab;

    public string Name => !string.IsNullOrWhiteSpace(name) ? name : prefab.name;
}

[System.Serializable]
public struct PrefabGroup {
    public string groupTitle;
    public DataPrefab[] prefabs;
}

[System.Serializable]
public struct NamedTextAsset {
    public string name;
    public TextAsset file;
}

[System.Serializable]
public struct NamedSprite {
    public string name;
    public Sprite sprite;
}

[System.Serializable]
public struct SMAssetGroup {
    public string groupTitle;
    public NamedTextAsset[] assets;
}

}