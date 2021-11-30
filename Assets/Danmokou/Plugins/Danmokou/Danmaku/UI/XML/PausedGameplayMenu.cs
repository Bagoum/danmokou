using System;
using System.Threading.Tasks;
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

    protected virtual void ShowMe() {
        if (!MenuActive) {
            tokens.Add(pauseToken = EngineStateManager.RequestState(EngineState.MENU_PAUSE));
            var disable = UpdatesEnabled.AddConst(false);
            _ = manager.FadeInPauseUI().ContinueWithSync(disable.Dispose);
            ServiceLocator.SFXService.Request(openPauseSound);
            Open();
        }
    }

    protected virtual Task HideMe() {
        pauseToken?.Dispose();
        return MenuActive ? Close() : Task.CompletedTask;
    }

    protected void ProtectHide() {
        if (MenuActive) {
            var disable = UpdatesEnabled.AddConst(false);
            ServiceLocator.SFXService.Request(closePauseSound);
            _ = manager.FadeOutPauseUI().ContinueWithSync(() => {
                _ = HideMe().ContinueWithSync(disable.Dispose);
            });
        }
    }
}
}