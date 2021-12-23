using System;
using Danmokou.Core;
using Danmokou.Services;
using SuzunoyaUnity;
using SuzunoyaUnity.UI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Danmokou.VN.Mimics {
public class DMKADVDialogueBoxMimic : ADVDialogueBoxMimic {
    
    public Image background = null!;

    public override void Initialize(ADVDialogueBox db) {
        base.Initialize(db);

        Listen(SaveData.s.DialogueOpacityEv, f => background.color = background.color.WithA(f));
        
        if (!bound.Container.AutoplayFastforwardAllowed && ((DMKVNState)bound.Container).AllowFullSkip) {
            if (skipButton != null) {
                skipButton.EnableButton();
                skipButton.text.text = "Full Skip";
                skipButton.onClicked.AddListener(FullSkip);
            }
        }
    }
    
    public virtual void FullSkip() {
        if (bound.Container.SkippingMode == null)
            InputManager.ExternalUISkipAllDialogue.SetForFrame();
        else
            bound.Container.SetSkipMode(null);
    }
}
}