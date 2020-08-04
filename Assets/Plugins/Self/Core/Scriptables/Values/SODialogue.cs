using System;
using UnityEngine;



//Inspector-exposed structs cannot be readonly
[Serializable]
public struct Translateable {
    public string name;
    public Translated[] files;
}

[Serializable]
public struct Translated {
    public Locale locale;
    public TextAsset file;
}

[CreateAssetMenu(menuName = "Data/Dialogue")]
public class SODialogue : ScriptableObject {
    public Translateable[] assetGroups;
}
