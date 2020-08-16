using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;
using UnityEngine.Serialization;

public class SFXService : MonoBehaviour {
    [Serializable]
    public struct ProcAudioHandler {
        public string name;
        public SOProccable proccer;
    }
    private static AudioSource src;
    private static SFXService main;
    public SFXConfig lifeExtend;
    public SFXConfig phaseEndFail;
    public SFXConfig phaseEndSuccess;
    public SFXConfig stageSectionEnd;
    public ProcAudioHandler[] procAudios;
    public SFXConfig[] SFX;
    private static readonly Dictionary<string, SFXConfig> dclips = new Dictionary<string, SFXConfig>();
    private static readonly Dictionary<string, SOProccable> dcont = new Dictionary<string, SOProccable>();
    private void Awake() {
        main = this;
        src = GetComponent<AudioSource>();
        dclips.Clear();
        for (int ii = 0; ii < SFX.Length; ++ii) {
            dclips[SFX[ii].defaultName] = SFX[ii];
        }
        dcont.Clear();
        for (int ii = 0; ii < procAudios.Length; ++ii) {
            dcont[procAudios[ii].name] = procAudios[ii].proccer;
        }
    }

    public void Update() {
        _timeouts.Clear();
        foreach (var kv in timeouts) {
            float v = kv.Value - ETime.dT;
            if (v > 0f) _timeouts[kv.Key] = v;
        }
        (timeouts, _timeouts) = (_timeouts, timeouts);
    }

    private static Dictionary<string, float> timeouts = new Dictionary<string,float>();
    private static Dictionary<string, float> _timeouts = new Dictionary<string,float>();

    public static void Request(string style) {
        if (string.IsNullOrWhiteSpace(style) || style == "_") return;
        if (timeouts.ContainsKey(style)) return;
        if (dcont.ContainsKey(style)) {
            dcont[style].Proc();
        } else if (dclips.ContainsKey(style)) {
            Request(dclips[style]);
        } else throw new Exception($"No SFX exists by name {style}");
    }

    public static Expression Request(Expression style) => request.Of(style);

    private static readonly ExFunction request = ExUtils.Wrap<SFXService>("Request", new[] {typeof(string)});

    public static void Request(SFXConfig aci) {
        if (timeouts.ContainsKey(aci.defaultName)) return;
        if (aci.Timeout > 0f) timeouts[aci.defaultName] = aci.Timeout;
        src.PlayOneShot(aci.clip, aci.volume);
    }

    public static void PhaseEndSound(bool? success) {
        if (success.HasValue) PhaseEndSound(success.Value);
    }

    public static void PhaseEndSound(bool success) => Request(success ? main.phaseEndSuccess : main.phaseEndFail);

    public static void StageSectionEndSound() => Request(main.stageSectionEnd);

    public static void LifeExtend() => Request(main.lifeExtend);
}
