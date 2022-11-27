using System;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Scenes;
using Danmokou.Services;
using Danmokou.UI.XML;
using JetBrains.Annotations;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;

namespace Danmokou.VN {
public class DMKVNState : UnityVNState {
    public readonly string id;
    public DMKVNState(ICancellee extCToken, string id, InstanceData save) : base(extCToken, save) {
        if (LoadTo != null)
            ServiceLocator.FindOrNull<ICameraTransition>()?.StallFadeOutUntil(() => SkippingMode != SkipMode.LOADING);
        this.id = id;
        AutoplayFastforwardAllowed = !Replayer.RequiresConsistency;
        AllowFullSkip = GameManagement.Instance.Request?.lowerRequest.Campaign.AllowDialogueSkip ?? false;
        Tokens.Add(SaveData.SettingsEv.Subscribe(s => {
            FastforwardReadTextOnly = s.VNOnlyFastforwardReadText;
        }));
        TimePerAutoplayConfirm = 1.5f;
        TimePerFastforwardConfirm = 0.15f;
    }

    public override bool ClickConfirmOrSkip() {
        InputManager.InCodeInput.mDialogueConfirm.SetActive();
        return true;
    }

    public LazyAction SFX(string? sfx) => new(aSFX(sfx));

    public Action aSFX(string? sfx) => () => {
        if (SkippingMode is null or SkipMode.AUTOPLAY)
            ISFXService.SFXService.Request(sfx);
    };
    
    /// <summary>
    /// Get a looping audio sfx.
    /// </summary>
    public AudioSource Source(string? sfx) => 
        ISFXService.SFXService.RequestSource(sfx, CToken)!;
    
    public LazyAction Source(string? sfx, Action<AudioSource> apply) => new(() => 
        apply(ISFXService.SFXService.RequestSource(sfx, CToken)!));
    
    public VNOperation Wait(double d) => base.Wait((float) d);

    public override void PauseGameplay() {
        ServiceLocator.Find<IPauseMenu>().QueueOpen();
    }

    public override void OpenLog() {
        ServiceLocator.Find<IVNBacklog>().QueueOpen();
    }

    public class RunningAudioTrackProxy : IDisposable {
        private readonly IRunningAudioTrack track;
        private readonly DMKVNState vn;

        public RunningAudioTrackProxy(IRunningAudioTrack track, DMKVNState vn) {
            this.track = track;
            this.vn = vn;
        }

        public void FadeOut(float time = 3f) {
            if (vn.SkippingMode == SkipMode.LOADING)
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
        var track = ServiceLocator.Find<IAudioTrackService>().InvokeBGM(key, new BGMInvokeFlags(SkippingMode != null ? 0 : 2)) ?? 
                    throw new Exception($"No track for key {key}");
        var proxy = new RunningAudioTrackProxy(track, this);
        Tokens.Add(proxy);
        return proxy;
    }

    public override string ToString() => $"{id}:{base.ToString()}";
}
}