using BagoumLib;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;

namespace Danmokou.Core {
public static class Helpers {
    public static CameraTransitionConfigRec AsQuickFade(this ICameraTransitionConfig ctc, bool silent = false) {
        var ss = ServiceLocator.Find<IScreenshotter>().Screenshot(DMKMainCamera.AllCameraTypes);
        return new(ss, ctc.FadeIn.AsInstantaneous, silent ? ctc.FadeOut.AsSilent : ctc.FadeOut) 
            { OnTransitionComplete = ss.DestroyTexOrRT };
    }
}
}