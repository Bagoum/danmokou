using System;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public class PausedGameplayMenu : UIController {
    public UIManager manager = null!;
    
    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    private IDisposable? pauseToken;

    protected override bool OpenOnInit => false;

    protected void ShowMe() {
        if (!MenuActive) {
            tokens.Add(pauseToken = EngineStateManager.RequestState(EngineState.MENU_PAUSE));
            var disable = UpdatesEnabled.AddConst(false);
            _ = manager.FadeInPauseUI().ContinueWithSync(disable.Dispose);
            ServiceLocator.SFXService.Request(openPauseSound);
            Open();
        }
    }

    protected virtual void HideMe() {
        if (MenuActive) {
            Close();
            pauseToken?.Dispose();
        }
    }

    protected void ProtectHide(Action? hide = null) {
        if (MenuActive) {
            var disable = UpdatesEnabled.AddConst(false);
            ServiceLocator.SFXService.Request(closePauseSound);
            _ = manager.FadeOutPauseUI().ContinueWithSync(() => {
                disable.Dispose();
                (hide ?? HideMe)();
            });
        }
    }
}
}