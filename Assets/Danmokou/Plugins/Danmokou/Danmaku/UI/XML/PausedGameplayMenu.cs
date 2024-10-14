using System;
using System.Collections;
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
    private IDisposable? pauseToken;

    protected override bool OpenOnInit => false;

    protected override void OnWillOpen() {
        ISFXService.SFXService.Request(XMLUtils.Prefabs.OpenPauseSound);
        tokens.Add(pauseToken = EngineStateManager.RequestState(EngineState.MENU_PAUSE));
    }

    protected override void OnWillClose() {
        ISFXService.SFXService.Request(XMLUtils.Prefabs.ClosePauseSound);
    }
    protected override void OnClosed() {
        pauseToken?.Dispose();
    }

    protected void ShowMeAfterFrames(int frames, float? time = null) {
        IEnumerator WaitThenShow() {
            for (int ii = 0; ii < frames; ++ii)
                yield return null;
            OpenWithAnimation(UIRoot.FadeTo(1, time ?? 0.3f, x => x).Run(this));
        }
        RunDroppableRIEnumerator(WaitThenShow());
    }
}
}