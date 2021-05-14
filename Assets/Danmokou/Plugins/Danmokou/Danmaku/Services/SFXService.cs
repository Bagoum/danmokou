using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Services {
public class SFXService : RegularUpdater, ISFXService {
    private static AudioSource src = null!;
    public SFXConfig lifeExtend = null!;
    public SFXConfig phaseEndFail = null!;
    public SFXConfig phaseEndSuccess = null!;
    public SFXConfig stageSectionEnd = null!;
    public SFXConfig powerLost = null!;
    public SFXConfig powerGained = null!;
    public SFXConfig powerFull = null!;
    public SFXConfig bossSpellCutin = null!;
    public SFXConfig bossCutin = null!;
    public SFXConfig bossExplode = null!;
    public SFXConfig meterUsable = null!;
    public SFXConfig meterActivated = null!;
    public SFXConfig meterDeActivated = null!;
    public SFXConfig swapHPScore = null!;
    public SFXConfig rankUp = null!;
    public SFXConfig rankDown = null!;
    public SFXConfig[] SFX = null!;
    private static readonly Dictionary<string, SFXConfig> dclips = new Dictionary<string, SFXConfig>();

    public readonly struct ConstructedAudio {
        public readonly AudioSource csrc;
        public readonly SFXConfig sfx;

        public ConstructedAudio(AudioSource csrc, SFXConfig sfx) {
            this.csrc = csrc;
            this.sfx = sfx;
        }
    }

    private static readonly CompactingArray<ConstructedAudio> constructed = new CompactingArray<ConstructedAudio>();

    public void Setup() {
        src = GetComponent<AudioSource>();
        dclips.Clear();
        for (int ii = 0; ii < SFX.Length; ++ii) {
            dclips[SFX[ii].defaultName] = SFX[ii];
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterDI<ISFXService>(this);
        
        Listen(Events.GameStateHasChanged, HandleGameStateChange);
        Listen(InstanceData.RankLevelChanged, increase => Request(increase ? rankUp : rankDown));
        Listen(InstanceData.MeterNowUsable, () => Request(meterUsable));
        Listen(InstanceData.AnyExtendAcquired, () => Request(lifeExtend));
        Listen(InstanceData.PhaseCompleted, pc => {
            if (pc.Captured.Try(out var captured)) {
                Request(captured ? phaseEndSuccess : phaseEndFail);
            } else if (pc.props.phaseType == PhaseType.STAGE && pc.props.Cleanup) {
                Request(stageSectionEnd);
            }
        });
        Listen(InstanceData.PowerFull, () => Request(powerFull));
        Listen(InstanceData.PowerGained, () => Request(powerGained));
        Listen(InstanceData.PowerLost, () => Request(powerLost));
        Listen(InstanceData.LifeSwappedForScore, () => Request(swapHPScore));

        Listen(Player.PlayerController.PlayerActivatedMeter, () => Request(meterActivated));
        Listen(Player.PlayerController.PlayerDeactivatedMeter, () => Request(meterDeActivated));
    }

    public void Update() {
        if (EngineStateManager.IsLoadingOrPaused) return;
        _timeouts.Clear();
        foreach (var kv in timeouts) {
            float v = kv.Value - ETime.dT;
            if (v > 0f) _timeouts[kv.Key] = v;
        }
        (timeouts, _timeouts) = (_timeouts, timeouts);
        for (int ii = 0; ii < constructed.Count; ++ii) {
            var c = constructed[ii];
            if (!c.csrc.isPlaying) {
                Destroy(c.csrc);
                constructed.Delete(ii);
            } else {
                if (c.sfx.slowable) {
                    c.csrc.pitch = c.sfx.Pitch;
                }
            }
        }
        constructed.Compact();

        for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
            loopTimeoutsArr[ii].Update(ETime.dT);
        }
    }

    public override void RegularUpdate() { }

    private class LoopingSourceInfo {
        public readonly AudioSource source;
        public bool active;
        private readonly float loopTimeCheck;
        public float timeUntilCheck;
        public bool requested;
        private readonly SFXConfig sfx;

        public LoopingSourceInfo(AudioSource src, SFXConfig sfx) {
            source = src;
            active = false;
            this.sfx = sfx;
            loopTimeCheck = sfx.loopTimeCheck;
            timeUntilCheck = 0;
            requested = false;
        }

        private float FeaturePitchMult() {
            if (sfx.feature == SFXConfig.LoopFeature.PLAYER_FIRE_HIT) {
                float lowHP = Counter.LowHPRequested ? 1.3f : 1f;
                float shotgun = Mathf.Lerp(1f, 0.7f, Counter.Shotgun);
                return lowHP * shotgun;
            } else return 1f;
        }

        private void SetProps() {
            source.volume = sfx.volume * SaveData.s.SEVolume;
            source.pitch = sfx.Pitch * FeaturePitchMult();
        }

        public void Request() {
            if (!active) {
                source.loop = true;
                if (!source.isPlaying) {
                    SetProps();
                    source.Play();
                }
                active = true;
                timeUntilCheck = loopTimeCheck;
                requested = false;
            } else requested = true;
        }

        public void Update(float dT) {
            if (active) {
                SetProps();
                if ((timeUntilCheck -= dT) <= 0) {
                    if (requested) {
                        timeUntilCheck = loopTimeCheck;
                        requested = false;
                    } else {
                        source.loop = false;
                        active = false;
                    }
                }
            }
        }
    }

    private static Dictionary<string, float> timeouts = new Dictionary<string, float>();
    private static Dictionary<string, float> _timeouts = new Dictionary<string, float>();
    private static readonly Dictionary<string, LoopingSourceInfo> loopTimeouts =
        new Dictionary<string, LoopingSourceInfo>();
    private static readonly List<LoopingSourceInfo> loopTimeoutsArr = new List<LoopingSourceInfo>();

    
    public void Request(string? style) {
        if (string.IsNullOrWhiteSpace(style) || style == "_" || style == null) return;
        if (timeouts.ContainsKey(style)) return;
        if (dclips.ContainsKey(style)) {
            Request(dclips[style]);
        } else throw new Exception($"No SFX exists by name {style}");
    }

    public void Request(SFXConfig? aci) {
        if (aci == null) return;
        if (aci.loop) {
            RequestLoop(aci);
            return;
        }
        if (timeouts.ContainsKey(aci.defaultName)) return;
        if (aci.Timeout > 0f) timeouts[aci.defaultName] = aci.Timeout;

        if (aci.RequiresHandling) RequestSource(aci);
        else src.PlayOneShot(aci.clip, aci.volume * SaveData.s.SEVolume);
    }

    /// <summary>
    /// Creates a looping audio effect that needs to repeatedly be requested in order to continue playing.
    /// </summary>
    /// <param name="aci"></param>
    private void RequestLoop(SFXConfig aci) {
        if (!loopTimeouts.TryGetValue(aci.defaultName, out var looper)) {
            var _src = _RequestSource(aci);
            if (_src == null) return;
            looper = new LoopingSourceInfo(_src, aci);
            looper.source.Play();
            loopTimeoutsArr.Add(looper);
            loopTimeouts[aci.defaultName] = looper;
        }
        looper.Request();
    }

    /// <summary>
    /// Returns an inactive source that is not playing and not tracked by SFXService.
    /// </summary>
    private AudioSource? _RequestSource(SFXConfig? aci) {
        if (aci == null) return null;
        var cmp = gameObject.AddComponent<AudioSource>();
        cmp.volume = aci.volume * SaveData.s.SEVolume;
        cmp.pitch = aci.Pitch;
        cmp.priority = aci.Priority;
        cmp.clip = aci.clip;
        return cmp;
    }

    public AudioSource? RequestSource(SFXConfig? aci) {
        if (aci == null) return null;
        var cmp = _RequestSource(aci);
        if (cmp != null) {
            constructed.AddV(new ConstructedAudio(cmp, aci));
            if (aci.loop)
                cmp.loop = true;
            cmp.Play();
        }
        return cmp;
    }

    public void RequestSFXEvent(ISFXService.SFXEventType ev) {
        if      (ev == ISFXService.SFXEventType.BossCutin)
            Request(bossCutin);
        else if (ev == ISFXService.SFXEventType.BossSpellCutin)
            Request(bossSpellCutin);
        else if (ev == ISFXService.SFXEventType.BossExplode)
            Request(bossExplode);
    }

    public static void ClearConstructed() {
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (constructed[ii].csrc != null) Destroy(constructed[ii].csrc);
        }
        for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
            Destroy(loopTimeoutsArr[ii].source);
        }
        constructed.Empty();
        loopTimeouts.Clear();
        loopTimeoutsArr.Clear();
    }

    private void HandleGameStateChange(EngineState state) {
        if (state.IsPaused()) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed[ii].sfx.Pausable) constructed[ii].csrc.Pause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.Pause();
            }
        } else if (state == EngineState.RUN) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed[ii].sfx.Pausable) constructed[ii].csrc.UnPause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.UnPause();
            }
        }
    }
}
}
