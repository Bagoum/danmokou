using System;
using System.Reactive;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Services;

namespace Danmokou.MiniProjects {
public class THJam13CustomDataFeature : BaseInstanceFeature, ICustomDataFeature, IDisposable {
    public InstanceData Inst { get; }
    public float TimeInMode { get; private set; }
    public float TimeInLastMode { get; private set; }
    public bool RetroMode => RetroModeEv.Value;
    public float RetroMode01 => RetroMode ? 1 : 0;
    public float RetroMode01Smooth => RetroMode01SmoothEv;
    private PushLerper<float> RetroMode01SmoothEv { get; } = new(0.5f);
    public Evented<bool> RetroModeEv { get; } = new(false);
    public BulletManager.StyleSelector Vuln { get; } = new("*black*");
    public ICObservable<(BulletManager.StyleSelector, bool)?> PlayerVulnEv { get; }

    private void UpdateRetroMode() {
        RetroModeEv.PublishIfNotSame((Inst.TeamCfg?.SelectedIndex ?? 0) == 1);
    }
    public THJam13CustomDataFeature(InstanceData inst) {
        Inst = inst;
        UpdateRetroMode();
        Tokens.Add(Inst.TeamUpdated.Subscribe(_ => UpdateRetroMode()));
        Tokens.Add(RetroModeEv.Subscribe(retro => {
            TimeInLastMode = TimeInMode;
            TimeInMode = 0;
            RetroMode01SmoothEv.PushIfNotSame(retro ? 1 : 0);
            ServiceLocator.MaybeFind<IShaderCamera>().ValueOrNull()?.ShowPixelation(
                new(2f, _ => LocationHelpers.TruePlayerLocation, pi => 16 * pi.t, 
                    retro ? pi => BMath.Lerp(640, 960, pi.t/2f) : null, retro));
        }));
        PlayerVulnEv = RetroModeEv.Select(retro => (Vuln, !retro) as (BulletManager.StyleSelector, bool)?);
        Tokens.Add(PlayerController.CollisionsForPool.AddDisturbance(PlayerVulnEv));
    }

    public void OnPlayerFrame(bool lenient, PlayerController.PlayerState state) {
        TimeInMode += ETime.FRAME_TIME;
        RetroMode01SmoothEv.Update(ETime.FRAME_TIME);
    }

    void IDisposable.Dispose() {
        RetroModeEv.OnCompleted();
        Tokens.DisposeAll();
    }
}

public class THJam13CustomDataFeatureCreator : IFeatureCreator<ICustomDataFeature> {
    public ICustomDataFeature Create(InstanceData instance) => new THJam13CustomDataFeature(instance);
}

}