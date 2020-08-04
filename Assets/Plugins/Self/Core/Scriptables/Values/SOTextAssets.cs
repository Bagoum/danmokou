using UnityEngine;

//Inspector-exposed structs cannot be readonly
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

[CreateAssetMenu(menuName = "Data/TextAssets")]
public class SOTextAssets : ScriptableObject {
    public SMAssetGroup[] assetGroups;
}
