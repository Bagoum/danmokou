using Danmokou.Core;
using SuzunoyaUnity;
using UnityEngine.UI;

namespace Danmokou.VN.Mimics {
public class DMKADVDialogueBoxMimic : ADVDialogueBoxMimic {
    
    public Image background = null!;

    public override void Initialize(ADVDialogueBox db) {
        base.Initialize(db);

        Listen(SaveData.s.DialogueOpacityEv, f => background.color = background.color.WithA(f));
    }
}
}