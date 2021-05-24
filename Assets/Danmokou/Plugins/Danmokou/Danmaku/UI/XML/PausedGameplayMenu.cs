using System;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public class PausedGameplayMenu : XMLMenu {
    public UIManager manager = null!;
    public VisualTreeAsset UIScreen = null!;
    
    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    private IDeletionMarker? pauseToken;
    
    protected void ShowMe() {
        if (!MenuActive) {
            MenuActive = true;
            tokens.Add(pauseToken = EngineStateManager.RequestState(EngineState.MENU_PAUSE));
            var disable = Disabler.CreateToken1(MultiOp.Priority.ALL);
            manager.SlideInUI(() => disable.TryRevoke());
            DependencyInjection.SFXService.Request(openPauseSound);
            UITop.style.display = DisplayStyle.Flex;
            ResetCurrentNode();
            Redraw();
        }
    }

    protected virtual void HideMe() {
        pauseToken?.MarkForDeletion();
        UITop.style.display = DisplayStyle.None;
        MenuActive = false;
    }

    protected void ProtectHide(Action? hide = null) {
        var disable = Disabler.CreateToken1(MultiOp.Priority.ALL);
        DependencyInjection.SFXService.Request(closePauseSound);
        manager.UnpauseAnimator(() => {
            disable.TryRevoke();
            (hide ?? HideMe)();
        });
    }
}
}