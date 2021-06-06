using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Suzunoya References")]
public class SuzunoyaReferences : ScriptableObject {
    
    [Header("Suzunoya")] 
    public GameObject renderGroupMimic = null!;
    public GameObject[] entityMimics = null!;
}
}