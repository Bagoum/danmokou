using System;
using BagoumLib.Culture;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Dialogue {
//Inspector-exposed structs cannot be readonly
[Serializable]
public struct TranslateableDialogue {
    public string name;
    public TranslatedDialogue[] files;
}

[Serializable]
public struct TranslatedDialogue {
    public string? locale;
    public TextAsset file;
}

}