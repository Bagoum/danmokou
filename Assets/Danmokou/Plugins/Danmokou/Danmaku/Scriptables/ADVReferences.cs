using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/ADV References")]
public class ADVReferences : ScriptableObject {
    public Sprite evidenceTargetIcon = null!;
    [Header("Investigation Icons")] 
    public Sprite talkToIcon = null!;
    public Sprite talkToObjectIcon = null!;
    public Sprite mapCurrentIcon = null!;
    public Sprite mapNotCurrentIcon = null!;
}
}