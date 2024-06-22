using System;
using BagoumLib;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using SuzunoyaUnity;
using SuzunoyaUnity.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Danmokou.VN.Mimics {
public class DMKADVDialogueBoxMimic : ADVDialogueBoxMimic {
    public TMPLinkHandler? LinkHandler { get; private set; }
    public bool useDialogueBoxOpacitySetting = true;
    public Image background = null!;

    public override void Initialize(ADVDialogueBox db) {
        base.Initialize(db);
        LinkHandler = GetComponentInChildren<TMPLinkHandler>();

        if (useDialogueBoxOpacitySetting)
            Listen(SaveData.SettingsEv, s => background.color = background.color.WithA(s.VNDialogueOpacity));
        
        if (ServiceLocator.FindOrNull<IPauseMenu>() == null)
            if (pauseButton != null)
                pauseButton.DisableButton();
        
        if (!bound.Container.AutoplayFastforwardAllowed && ((DMKVNState)bound.Container).AllowFullSkip) {
            if (skipButton != null) {
                skipButton.EnableButton();
                skipButton.text.text = "Full Skip";
                skipButton.onClicked.AddListener(FullSkip);
            }
        }
        
        foreach (var b in buttons)
            if (b is DMKDialogueBoxButton d)
                d.Bind(this, db);
    }
    
    public virtual void FullSkip() {
        if (bound.Container.SkippingMode == null)
            InputManager.InCodeInput.mDialogueSkipAll.SetActive();
        else
            bound.Container.SetSkipMode(null);
    }
}
}