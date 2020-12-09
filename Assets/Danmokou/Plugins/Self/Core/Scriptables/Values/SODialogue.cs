using System;
using DMK.Dialogue;
using UnityEngine;


namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Dialogue")]
public class SODialogue : ScriptableObject {
    public TranslateableDialogue[] assetGroups;
}
}
