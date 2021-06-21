using System;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
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
            _ = manager.FadeInPauseUI().ContinueWithSync(() => disable.TryRevoke());
            DependencyInjection.SFXService.Request(openPauseSound);
            UI.style.display = DisplayStyle.Flex;
            ResetCurrentNode();
            Redraw();
        }
    }

    protected virtual void HideMe() {
        if (MenuActive) {
            MenuActive = false;
            MainScreen.ResetNodeProgress();
            pauseToken?.MarkForDeletion();
            UI.style.display = DisplayStyle.None;
        }
    }

    protected void ProtectHide(Action? hide = null) {
        if (MenuActive) {
            var disable = Disabler.CreateToken1(MultiOp.Priority.ALL);
            DependencyInjection.SFXService.Request(closePauseSound);
            _ = manager.FadeOutPauseUI().ContinueWithSync(() => {
                disable.TryRevoke();
                (hide ?? HideMe)();
            });
        }
    }
}
}