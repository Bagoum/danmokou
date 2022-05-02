using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/ADV References")]
public class ADVReferences : ScriptableObject {
    [Header("Investigation Icons")] 
    public Sprite talkToIcon = null!;
    public Sprite mapCurrentIcon = null!;
    public Sprite mapNotCurrentIcon = null!;
}
}