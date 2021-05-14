using Danmokou.Core;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/TextAssets")]
public class SOTextAssets : ScriptableObject {
    public SMAssetGroup[] assetGroups = null!;
}
}