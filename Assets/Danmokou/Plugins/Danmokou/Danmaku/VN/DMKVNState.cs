using System;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.UI.XML;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;

namespace Danmokou.VN {
public class DMKVNState : UnityVNState {
    public DMKVNState(ICancellee extCToken, string? scriptId = null, InstanceData? save = null) : base(extCToken,
        scriptId, save) { }

    public LazyAction lSFX(string? sfx) => new LazyAction(aSFX(sfx));

    public Action aSFX(string? sfx) => () => {
        if (!Skipping)
            DependencyInjection.SFXService.Request(sfx);
    };

    public VNOperation Wait(double d) => base.Wait((float) d);

    public override void PauseGameplay() {
        DependencyInjection.Find<IPauseMenu>().Open();
    }

    public override void OpenLog() {
        DependencyInjection.Find<IVNBacklog>().Open();
    }

    public class RunningAudioTrackProxy : IDisposable {
        private readonly IRunningAudioTrack track;
        private readonly DMKVNState vn;

        public RunningAudioTrackProxy(IRunningAudioTrack track, DMKVNState vn) {
            this.track = track;
            this.vn = vn;
        }

        public void FadeOut(float time = 3f) {
            if (vn.ExecCtx.LoadSkipping)
                track.Cancel();
            else
                track.FadeOutThenDestroy(time);
        }

        public void FadeIn(float time = 3f) => track.FadeIn(time);

        public void Dispose() {
            if (!track.Active.Cancelled)
                track.Cancel();
        }
    }

    public RunningAudioTrackProxy RunBGM(string key) {
        var track = AudioTrackService.InvokeBGM(key, new BGMInvokeFlags(Skipping ? 0 : 2)) ?? 
                    throw new Exception($"No track for key {key}");
        var proxy = new RunningAudioTrackProxy(track, this);
        Tokens.Add(proxy);
        return proxy;
    }
    
}
}