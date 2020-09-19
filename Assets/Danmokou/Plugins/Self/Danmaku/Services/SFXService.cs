using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

public class SFXService : MonoBehaviour {
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
    public SFXConfig[] SFX;
    private static readonly Dictionary<string, SFXConfig> dclips = new Dictionary<string, SFXConfig>();
    private static readonly CompactingArray<AudioSource> loopSources = new CompactingArray<AudioSource>();
    private static readonly List<AudioSource> constructed = new List<AudioSource>();
    public void Setup() {
        main = this;
        src = GetComponent<AudioSource>();
        dclips.Clear();
        for (int ii = 0; ii < SFX.Length; ++ii) {
            dclips[SFX[ii].defaultName] = SFX[ii];
        }
    }

    public void Update() {
        if (GameStateManager.IsLoadingOrPaused) return;
        _timeouts.Clear();
        foreach (var kv in timeouts) {
            float v = kv.Value - ETime.dT;
            if (v > 0f) _timeouts[kv.Key] = v;
        }
        (timeouts, _timeouts) = (_timeouts, timeouts);
        for (int ii = 0; ii < loopSources.Count; ++ii) {
            if (!loopSources[ii].isPlaying) {
                Destroy(loopSources[ii]);
                loopSources.Delete(ii);
            }
        }
        loopSources.Compact();

        for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
            loopTimeoutsArr[ii].Update(ETime.dT);
        }
    }

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
        public void Request() {
            if (!active) {
                source.loop = true;
                if (!source.isPlaying) source.Play();
                active = true;
                timeUntilCheck = loopTimeCheck;
                requested = false;
            } else requested = true;
        }
        public void Update(float dT) {
            if (active) {
                source.pitch = sfx.pitch * FeaturePitchMult();
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
    private static Dictionary<string, LoopingSourceInfo> loopTimeouts = new Dictionary<string, LoopingSourceInfo>();
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
        src.PlayOneShot(aci.clip, aci.volume);
    }

    private static void RequestLoop(SFXConfig aci) {
        if (!loopTimeouts.TryGetValue(aci.defaultName, out var looper)) {
            looper = new LoopingSourceInfo(RequestSource(aci), aci);
            loopTimeoutsArr.Add(looper);
            loopTimeouts[aci.defaultName] = looper;
        }
        looper.Request();
    }

    [CanBeNull]
    public static AudioSource RequestSource([CanBeNull] SFXConfig aci) {
        if (aci == null) return null;
        var cmp = main.gameObject.AddComponent<AudioSource>();
        constructed.Add(cmp);
        cmp.volume = aci.volume;
        cmp.pitch = aci.pitch;
        cmp.priority = aci.Priority;
        cmp.clip = aci.clip;
        cmp.Play();
        return cmp;
    }

    /// <summary>
    /// External-facing, though may soon be deprecated
    /// </summary>
    public static AudioSource RequestLoopingSource([CanBeNull] SFXConfig aci) {
        var s = RequestSource(aci);
        if (s != null) {
            s.loop = true;
            loopSources.Add(ref s);
        }
        return s;
    }

    public static void PhaseEndSound(bool? success) {
        if (success.HasValue) PhaseEndSound(success.Value);
    }

    public static void PhaseEndSound(bool success) => Request(success ? main.phaseEndSuccess : main.phaseEndFail);

    public static void StageSectionEndSound() => Request(main.stageSectionEnd);

    public static void LifeExtend() => Request(main.lifeExtend);


    public static void PowerLost() => Request(main.powerLost);
    public static void PowerGained() => Request(main.powerGained);
    public static void PowerFull() => Request(main.powerFull);
    public static void BossSpellCutin() => Request(main.bossSpellCutin);
    public static void BossCutin() => Request(main.bossCutin);
    public static void BossExplode() => Request(main.bossExplode);
    public static void MeterUsable() => Request(main.meterUsable);
    public static void MeterActivated() => Request(main.meterActivated);
    
    
    
    public static void ClearConstructed() {
        for (int ii = 0; ii < constructed.Count; ++ii) {
            if (constructed[ii] != null) Destroy(constructed[ii]);
        }
        constructed.Clear();
        loopSources.Empty(true);
        loopTimeouts.Clear();
        loopTimeoutsArr.Clear();
    }
    
    private DeletionMarker<Action<GameState>> gameStateListener;
    protected void OnEnable() {
        gameStateListener = Core.Events.GameStateHasChanged.Listen(HandleGameStateChange);
    }
    protected void OnDisable() {
        gameStateListener.MarkForDeletion();
    }

    private void HandleGameStateChange(GameState state) {
        if (state.IsPaused()) {
            for (int ii = 0; ii < loopSources.Count; ++ii) {
                loopSources[ii].Pause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.Pause();
            }
        } else if (state == GameState.RUN) {
            for (int ii = 0; ii < loopSources.Count; ++ii) {
                loopSources[ii].UnPause();
            }
            for (int ii = 0; ii < loopTimeoutsArr.Count; ++ii) {
                loopTimeoutsArr[ii].source.UnPause();
            }
        }
    }
    
}
