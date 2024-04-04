using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
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
    private AudioSource src = null!;
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
    public SFXConfig meterUseFail = null!;
    public SFXConfig swapHPScore = null!;
    public SFXConfig flakeItemCollected = null!;
    public SFXConfig rankUp = null!;
    public SFXConfig rankDown = null!;
    public SFXConfig[] SFX = null!;
    private readonly Dictionary<string, SFXConfig> dclips = new();

    public readonly struct ConstructedAudio {
        public readonly AudioSource csrc;
        public readonly SFXConfig sfx;
        public readonly ICancellee cT;

        public ConstructedAudio(AudioSource csrc, SFXConfig sfx, ICancellee cT) {
            this.csrc = csrc;
            this.sfx = sfx;
            this.cT = cT;
        }
    }
    private readonly DMCompactingArray<ConstructedAudio> constructed = new();

    public void Setup() {
        src = GetComponent<AudioSource>();
        dclips.Clear();
        foreach (var configs in GameManagement.References.SFX.Select(x => x.sfxs).Prepend(SFX)) {
            for (int ii = 0; ii < configs.Length; ++ii) {
                dclips[configs[ii].defaultName] = configs[ii];
            }
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<ISFXService>(this);

#if UNITY_EDITOR || ALLOW_RELOAD
        Listen(Events.LocalReset, ClearConstructed);
#endif
        Listen(Events.SceneCleared, ClearConstructed);
        Listen(EngineStateManager.EvState, HandleEngineStateChange);

        Listen(PlayerController.PlayerActivatedMeter, () => Request(meterActivated));
        Listen(PlayerController.PlayerDeactivatedMeter, () => Request(meterDeActivated));
        Listen(PlayerController.PlayerMeterFailed, () => Request(meterUseFail));

        Listen(GameManagement.EvInstance, i => i.MeterF.MeterBecameUsable, () => Request(meterUsable));
        Listen(GameManagement.EvInstance, i => i.ExtendAcquired, _ => Request(lifeExtend));
        Listen(GameManagement.EvInstance, i => i.PhaseCompleted, pc => {
            if (pc.phase.Props.endSound) {
                if (pc.Captured.Try(out var captured)) {
                    Request(captured ? phaseEndSuccess : phaseEndFail);
                } else if (pc.phase.Props.phaseType == PhaseType.Stage && pc.phase.Props.Cleanup) {
                    Request(stageSectionEnd);
                }
            }
        });
        Listen(GameManagement.EvInstance, i => i.PowerF.PowerFull, () => Request(powerFull));
        Listen(GameManagement.EvInstance, i => i.PowerF.PowerGained, () => Request(powerGained));
        Listen(GameManagement.EvInstance, i => i.PowerF.PowerLost, () => Request(powerLost));
        Listen(GameManagement.EvInstance, i => i.LifeSwappedForScore, () => Request(swapHPScore));
        Listen(GameManagement.EvInstance, i => i.RankF?.RankLevelChanged, increase => Request(increase ? rankUp : rankDown));
    }

    public void Update() {
        if (EngineStateManager.State != EngineState.RUN) return;
        _timeouts.Clear();
        foreach (var kv in timeouts) {
            float v = kv.Value - ETime.dT;
            if (v > 0f) _timeouts[kv.Key] = v;
        }
        (timeouts, _timeouts) = (_timeouts, timeouts);
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (constructed.GetIfExistsAt(ii, out var c)) {
                if (!c.csrc.isPlaying || c.cT.Cancelled) {
                    Destroy(c.csrc);
                    constructed.Delete(ii);
                } else {
                    if (c.sfx.slowable) {
                        c.csrc.pitch = c.sfx.Pitch;
                    }
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
                float shotgun = M.Lerp(1f, 0.7f, Counter.Shotgun);
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

    private Dictionary<string, float> timeouts = new();
    private Dictionary<string, float> _timeouts = new();
    private readonly Dictionary<string, LoopingSourceInfo> loopTimeouts =
        new();
    private readonly List<LoopingSourceInfo> loopTimeoutsArr = new();

    
    public void Request(string? style, SFXType type) {
        if (string.IsNullOrWhiteSpace(style) || style == "_" || style == null) return;
        if (timeouts.ContainsKey(style)) return;
        if (dclips.ContainsKey(style)) {
            Request(dclips[style], type);
        } else throw new Exception($"No SFX exists by name {style}");
    }

    public void Request(string? style) => Request(style, SFXType.Default);

    public void Request(SFXConfig? aci, SFXType type = SFXType.Default) {
        if (aci == null) return;
        if (aci.clip == null) {
            Logs.Log($"SFXConfig {aci} does not have any associated sound file.", true, level: LogLevel.WARNING);
            return;
        }
        if (aci.loop) {
            RequestLoop(aci);
            return;
        }
        if (timeouts.ContainsKey(aci.defaultName)) return;
        if (aci.Timeout > 0f) timeouts[aci.defaultName] = aci.Timeout;

        if (aci.RequiresHandling) RequestSource(aci, Cancellable.Null);
        else src.PlayOneShot(aci.clip, aci.volume * type switch {
            SFXType.TypingSound => SaveData.s.VNTypingSoundVolume,
            _ => SaveData.s.SEVolume
        });
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

    public AudioSource? RequestSource(SFXConfig? aci, ICancellee cT) {
        if (aci == null) return null;
        var cmp = _RequestSource(aci);
        if (cmp != null) {
            constructed.Add(new ConstructedAudio(cmp, aci, cT));
            if (aci.loop)
                cmp.loop = true;
            cmp.Play();
        }
        return cmp;
    }
    
    public AudioSource? RequestSource(string? style, ICancellee cT) {
        if (string.IsNullOrWhiteSpace(style) || style is "_" or null) return null;
        if (dclips.ContainsKey(style)) {
            return RequestSource(dclips[style], cT);
        } else throw new Exception($"No SFX exists by name {style}");
    }

    public void RequestSFXEvent(ISFXService.SFXEventType ev) {
        Request(ev switch {
            ISFXService.SFXEventType.BossCutin => bossCutin,
            ISFXService.SFXEventType.BossSpellCutin => bossSpellCutin,
            ISFXService.SFXEventType.BossExplode => bossExplode,
            ISFXService.SFXEventType.FlakeItemCollected => flakeItemCollected,
            _ => null
        });
    }

    private void ClearConstructed() {
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (constructed.GetIfExistsAt(ii, out var c) && c.csrc != null)
                Destroy(c.csrc);
        }
        for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
            Destroy(loopTimeoutsArr[ii].source);
        }
        constructed.Empty();
        loopTimeouts.Clear();
        loopTimeoutsArr.Clear();
    }

    private void HandleEngineStateChange(EngineState state) {
        if (state.IsPaused()) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed.GetIfExistsAt(ii, out var c))
                    c.csrc.Pause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.Pause();
            }
        } else if (state == EngineState.RUN) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed.GetIfExistsAt(ii, out var c))
                    c.csrc.UnPause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.UnPause();
            }
        }
    }
}
}
