using System;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using UnityEngine;

namespace Danmokou.Behavior {
public class ContinuousAudio : ProcReader {
    private AudioSource src = null!;
    private float baseVolume;

    [ReflectInto(typeof(FXY))]
    public string VolumeScaler = "";
    private ReflWrap<FXY> VolScale = null!;
    [ReflectInto(typeof(FXY))]
    public string SpeedScaler = "";
    private ReflWrap<FXY> speedScale = null!;
    protected virtual FXY SpeedScale => speedScale;

    protected virtual void Awake() {
        src = GetComponent<AudioSource>();
        baseVolume = src.volume;
        VolScale = new ReflWrap<FXY>(VolumeScaler);
        speedScale = new ReflWrap<FXY>(SpeedScaler);
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(EngineStateManager.EvState, HandleEngineStateChange);
    }

    public override int UpdatePriority => UpdatePriorities.SLOW;

    protected override void Check(int procs) {
        if (procs > 0) {
            if (!src.isPlaying) {
                src.Play();
            }
            src.loop = true;
            src.volume = baseVolume * VolScale.Value(procs) * SaveData.s.SEVolume;
            src.pitch = SpeedScale(procs);
        } else if (procs == 0 && src.isPlaying) {
            src.loop = false;
            //src.volume *= .5f;
        }
    }

    private void HandleEngineStateChange(EngineState state) {
        if (state.IsPaused()) {
            src.Stop();
            ResetProcs();
        }
    }
}
}
