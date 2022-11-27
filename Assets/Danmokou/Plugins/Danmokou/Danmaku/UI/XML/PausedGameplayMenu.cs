using System;
using System.Threading.Tasks;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public class PausedGameplayMenu : UIController {
    public SFXConfig? openPauseSound;
    public SFXConfig? closePauseSound;

    private IDisposable? pauseToken;

    protected override bool OpenOnInit => false;

    [ContextMenu("Animate show menu")]
    protected virtual void ShowMe() {
        if (!MenuActive) {
            tokens.Add(pauseToken = EngineStateManager.RequestState(EngineState.MENU_PAUSE));
            var disable = UpdatesEnabled.AddConst(false);
            _ = UIRoot.FadeTo(1, 0.3f, x=>x).Run(this).ContinueWithSync(disable.Dispose);
            ISFXService.SFXService.Request(openPauseSound);
            Open().ContinueWithSync();
        }
    }

    protected virtual Task HideMe() {
        pauseToken?.Dispose();
        return MenuActive ? Close() : Task.CompletedTask;
    }

    [ContextMenu("Animate hide menu")]
    protected void ProtectHide() {
        if (MenuActive) {
            var disable = UpdatesEnabled.AddConst(false);
            ISFXService.SFXService.Request(closePauseSound);
            _ = UIRoot.FadeTo(0, 0.3f, x=>x).Run(this).ContinueWithSync(() => {
                _ = HideMe().ContinueWithSync(disable.Dispose);
            });
        }
    }
}
}