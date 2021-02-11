using DMK.Core;
using UnityEngine;


namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/TextAssets")]
public class SOTextAssets : ScriptableObject {
    public SMAssetGroup[] assetGroups = null!;
}
}