using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

public class SFXService : RegularUpdater {
    private static AudioSource src;
    private static SFXService main;
    public SFXConfig lifeExtend;
    public SFXConfig phaseEndFail;
    public SFXConfig phaseEndSuccess;
    public SFXConfig stageSectionEnd;
    public SFXConfig powerLost;
    public SFXConfig powerGained;
    public SFXConfig powerFull;
    public SFXConfig bossSpellCutin;
    public SFXConfig bossCutin;
    public SFXConfig bossExplode;
    public SFXConfig meterUsable;
    public SFXConfig meterActivated;
    public SFXConfig meterDeActivated;
    public SFXConfig swapHPScore;
    public SFXConfig[] SFX;
    private static readonly Dictionary<string, SFXConfig> dclips = new Dictionary<string, SFXConfig>();

    public readonly struct ConstructedAudio {
        public readonly AudioSource src;
        public readonly bool pausable;
        public ConstructedAudio(AudioSource src, bool pausable) {
            this.src = src;
            this.pausable = pausable;
        }
    }
    private static readonly CompactingArray<ConstructedAudio> constructed = new CompactingArray<ConstructedAudio>();
    public void Setup() {
        main = this;
        src = GetComponent<AudioSource>();
        dclips.Clear();
        for (int ii = 0; ii < SFX.Length; ++ii) {
            dclips[SFX[ii].defaultName] = SFX[ii];
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Core.Events.GameStateHasChanged, HandleGameStateChange);
        Listen(CampaignData.MeterNowUsable, () => Request(meterUsable));
        Listen(CampaignData.AnyExtendAcquired, () => Request(lifeExtend));
        Listen(CampaignData.PhaseCompleted, pc => {
            if (pc.Captured.Try(out var captured)) {
                Request(captured ? main.phaseEndSuccess : main.phaseEndFail);
            } else if (pc.props.phaseType == Enums.PhaseType.STAGE && pc.props.Cleanup) {
                Request(main.stageSectionEnd);
            }
        });
        Listen(CampaignData.PowerFull, () => Request(main.powerFull));
        Listen(CampaignData.PowerGained, () => Request(main.powerGained));
        Listen(CampaignData.PowerLost, () => Request(main.powerLost));
        Listen(CampaignData.LifeSwappedForScore, () => Request(main.swapHPScore));

        Listen(PlayerInput.PlayerActivatedMeter, () => Request(meterActivated));
        Listen(PlayerInput.PlayerDeactivatedMeter, () => Request(meterDeActivated));
    }

    public void Update() {
        if (GameStateManager.IsLoadingOrPaused) return;
        _timeouts.Clear();
        foreach (var kv in timeouts) {
            float v = kv.Value - ETime.dT;
            if (v > 0f) _timeouts[kv.Key] = v;
        }
        (timeouts, _timeouts) = (_timeouts, timeouts);
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (!constructed[ii].src.isPlaying) {
                Destroy(constructed[ii].src);
                constructed.Delete(ii);
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
            source.pitch = sfx.pitch * FeaturePitchMult();
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
    private static Dictionary<string, float> timeouts = new Dictionary<string,float>();
    private static Dictionary<string, float> _timeouts = new Dictionary<string,float>();
    private static readonly Dictionary<string, LoopingSourceInfo> loopTimeouts = new Dictionary<string, LoopingSourceInfo>();
    private static readonly List<LoopingSourceInfo> loopTimeoutsArr = new List<LoopingSourceInfo>();

    public static void Request(string style) {
        if (string.IsNullOrWhiteSpace(style) || style == "_") return;
        if (timeouts.ContainsKey(style)) return;
        if (dclips.ContainsKey(style)) {
            Request(dclips[style]);
        } else throw new Exception($"No SFX exists by name {style}");
    }

    public static Expression Request(Expression style) => request.Of(style);

    private static readonly ExFunction request = ExUtils.Wrap<SFXService>("Request", new[] {typeof(string)});

    public static void Request([CanBeNull] SFXConfig aci) {
        if (aci == null) return;
        if (aci.loop) {
            RequestLoop(aci);
            return;
        }
        if (timeouts.ContainsKey(aci.defaultName)) return;
        if (aci.Timeout > 0f) timeouts[aci.defaultName] = aci.Timeout;
        
        if (aci.pausable) RequestSource(aci, true);
        else src.PlayOneShot(aci.clip, aci.volume * SaveData.s.SEVolume);
    }

    private static void RequestLoop(SFXConfig aci) {
        if (!loopTimeouts.TryGetValue(aci.defaultName, out var looper)) {
            looper = new LoopingSourceInfo(_RequestSource(aci), aci);
            looper.source.Play();
            loopTimeoutsArr.Add(looper);
            loopTimeouts[aci.defaultName] = looper;
        }
        looper.Request();
    }

    /// <summary>
    /// Returns an inactive source that is not playing and not tracked by SFXService.
    /// </summary>
    [CanBeNull]
    private static AudioSource _RequestSource([CanBeNull] SFXConfig aci) {
        if (aci == null) return null;
        var cmp = main.gameObject.AddComponent<AudioSource>();
        cmp.volume = aci.volume * SaveData.s.SEVolume;
        cmp.pitch = aci.pitch;
        cmp.priority = aci.Priority;
        cmp.clip = aci.clip;
        return cmp;
    }
    
    [CanBeNull]
    public static AudioSource RequestSource([CanBeNull] SFXConfig aci, bool pausable = true) {
        if (aci == null) return null;
        var cmp = _RequestSource(aci);
        if (cmp != null) {
            constructed.AddV(new ConstructedAudio(cmp, pausable));
            cmp.Play();
        }
        return cmp;
    }

    public static AudioSource RequestLoopingSource([CanBeNull] SFXConfig aci) {
        var s = RequestSource(aci);
        if (s != null) {
            s.loop = true;
        }
        return s;
    }
    
    public static void BossSpellCutin() => Request(main.bossSpellCutin);
    public static void BossCutin() => Request(main.bossCutin);
    public static void BossExplode() => Request(main.bossExplode);
    
    
    
    public static void ClearConstructed() {
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (constructed[ii].src != null) Destroy(constructed[ii].src);
        }
        for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
            Destroy(loopTimeoutsArr[ii].source);
        }
        constructed.Empty(true);
        loopTimeouts.Clear();
        loopTimeoutsArr.Clear();
    }

    private void HandleGameStateChange(GameState state) {
        if (state.IsPaused()) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed[ii].pausable) constructed[ii].src.Pause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.Pause();
            }
        } else if (state == GameState.RUN) {
            for (int ii = 0; ii < constructed.Count; ++ii) {
                if (constructed[ii].pausable) constructed[ii].src.UnPause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.UnPause();
            }
        }
    }
    
}
