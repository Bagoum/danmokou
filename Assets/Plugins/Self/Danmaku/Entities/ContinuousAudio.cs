using System;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

public class ContinuousAudio : ProcReader {
    private AudioSource src;
    private float baseVolume;

    public string VolumeScaler;
    private ReflWrap<FXY> VolScale;
    public string SpeedScaler;
    private ReflWrap<FXY> speedScale;
    protected virtual FXY SpeedScale => speedScale;

    protected virtual void Awake() {
        src = GetComponent<AudioSource>();
        baseVolume = src.volume;
        VolScale = (Func<FXY>) (VolumeScaler.Into<FXY>);
        speedScale = (Func<FXY>) (SpeedScaler.Into<FXY>);
    }

    public override int UpdatePriority => UpdatePriorities.SLOW;

    protected override void Check(int procs) {
        if (procs > 0) {
            if (!src.isPlaying) {
                src.Play();
            }
            src.loop = true;
            src.volume = baseVolume * VolScale.Value(procs);
            src.pitch = SpeedScale(procs);
        } else if (procs == 0 && src.isPlaying) {
            src.loop = false;
            //src.volume *= .5f;
        }
    }

    private DeletionMarker<Action<GameState>> gameStateListener;
    protected override void OnEnable() {
        gameStateListener = Core.Events.GameStateHasChanged.Listen(HandleGameStateChange);
        base.OnEnable();
    }

    protected override void OnDisable() {
        gameStateListener.MarkForDeletion();
        base.OnDisable();
    }

    private void HandleGameStateChange(GameState state) {
        if (state.IsPaused()) {
            src.Stop();
            ResetProcs();
        }
    }
}
