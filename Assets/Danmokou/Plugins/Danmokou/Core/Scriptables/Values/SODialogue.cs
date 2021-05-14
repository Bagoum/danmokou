using System;
using Danmokou.Dialogue;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Dialogue")]
public class SODialogue : ScriptableObject {
    public TranslateableDialogue[] assetGroups = null!;
}
}
