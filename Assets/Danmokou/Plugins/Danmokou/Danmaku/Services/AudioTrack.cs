using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scenes;
using UnityEngine;
using Object = System.Object;

namespace Danmokou.Services {
public interface IRunningAudioTrack {
    IAudioTrackInfo Track { get; }
    public void SetLocalVolume(BPY? volume);
    void RegularUpdateSrc();
    
    void PauseSrc();
    void UnPauseSrc();
    
    /// <summary>
    /// Destroy the track's resources on the next update.
    /// </summary>
    void CancelSrc();

    void DestroySrc();
}

/// <summary>
/// The current state of an audio track.
/// </summary>
public enum AudioTrackState {
    /// <summary>
    /// The audio track is playing.
    /// </summary>
    Active = 0,
    /// <summary>
    /// The audio track is in the process of pausing. This may involve a fade-out before <see cref="Paused"/>.
    /// </summary>
    Pausing = 1,
    /// <summary>
    /// The audio track is paused.
    /// </summary>
    Paused = 2,
    /// <summary>
    /// The audio track is in the process of being destroyed. This may involve a fade-out before <see cref="DestroyReady"/>.
    /// </summary>
    DestroyPrepare = 3,
    /// <summary>
    /// The audio track is done, and its resources can be fully disposed.
    /// </summary>
    DestroyReady = 4
}

public abstract class BaseRunningAudioTrack : IRunningAudioTrack {
    public IAudioTrackInfo Track { get; }

    protected readonly AudioTrackSet host;
    protected PushLerper<float> LocalVolume { get; } = new(0.3f);
    public BPY? Volume { get; private set; }
    protected DisturbedProduct<float> Src1Volume { get; }
    protected AudioSource currSrc;

    public BaseRunningAudioTrack(IAudioTrackInfo track, AudioTrackSet host) {
        Src1Volume = new DisturbedProduct<float>(track.Volume);
        LocalVolume.Push(1);
        host.Tokens.Add(Src1Volume.AddDisturbance(LocalVolume));
        host.Tokens.Add(Src1Volume.AddDisturbance(SaveData.s.BGMVolume));
        host.Tokens.Add(Src1Volume.AddDisturbance(host.Fader));
        Track = track;
        this.host = host;
        var src1 = currSrc = host.Srv.gameObject.AddComponent<AudioSource>();
        src1.clip = Track.Clip;
        src1.pitch = Track.Pitch;
        host.Tokens.Add(Src1Volume.Subscribe(v => src1.volume = v));
        Logs.Log($"Created audio track: {Track.Title}");
        host.Tracks.Add(this);
        if (host.State.Value >= AudioTrackState.Active)
            PlayDelayed();
    }
    
    //Delay initial audio source play. This is useful to avoid Unity bugs where the volume doesn't update
    // properly after creating a new source.
    protected void PlayDelayed(int frames=2) {
        IEnumerator _Inner() {
            if (host.BreakCoroutine) yield break;
            for (int ii = 0; ii < frames;) {
                yield return null;
                if (host.BreakCoroutine) yield break;
                if (host.UpdateCoroutine) ++ii;
            }
            Logs.Log($"Playing {this.GetType()} audio track {Track.Title} (delayed by {frames} frames)");
            currSrc.Play();
        }
        host.Srv.Run(_Inner());
    }

    public void SetLocalVolume(BPY? vol) {
        Volume = vol;
        if (vol != null) {
            LocalVolume.Unset();
            LocalVolume.Push(vol(host.Pi));
        }
    }

    public virtual void RegularUpdateSrc() {
        if (host.UpdateCoroutine) {
            if (Volume != null)
                LocalVolume.PushIfNotSame(Volume(host.Pi));
            LocalVolume.Update(ETime.FRAME_TIME);
        }
    }
    
    
    public virtual void PauseSrc() {
        Logs.Log($"Paused audio track: {Track.Title}", level: LogLevel.DEBUG1);
        currSrc.Pause();
    }
    public virtual void UnPauseSrc() {
        Logs.Log($"Unpaused audio track: {Track.Title}", level: LogLevel.DEBUG1);
        currSrc.UnPause();
    }
    
    public virtual void CancelSrc() {
        Logs.Log($"Cancelling audio track: {Track.Title}. It will be destroyed on the next frame.");
        Src1Volume.OnCompleted();
        currSrc.Stop();
    }
    
    public virtual void DestroySrc() {
        UnityEngine.Object.Destroy(currSrc);
    }
}
public class NaiveLoopRAT : BaseRunningAudioTrack {
    public NaiveLoopRAT(IAudioTrackInfo track, AudioTrackSet host) : base(track, host) {
        currSrc.loop = true;
        currSrc.time = Track.StartTime;
    }
}
public class TimedLoopRAT : BaseRunningAudioTrack {
    private const float xfadeTime = 0.3f;
    private const float xfadeOverlap = 0.2f;
    //Two sources are necessary to smoothly loop by crossfading the track into itself.
    private PushLerper<float> currSrcFadeVol = new(xfadeTime, M.Lerp);
    private PushLerper<float> nextSrcFadeVol = new(xfadeTime, M.Lerp);
    private DisturbedProduct<float> Src2Volume { get; }
    private AudioSource nextSrc;

    public TimedLoopRAT(IAudioTrackInfo track, AudioTrackSet host) : base(track, host) {
        currSrcFadeVol.Push(1);
        host.Tokens.Add(Src1Volume.AddDisturbance(currSrcFadeVol));
        nextSrcFadeVol.Push(0);
        Src2Volume = new DisturbedProduct<float>(track.Volume);
        host.Tokens.Add(Src2Volume.AddDisturbance(LocalVolume));
        host.Tokens.Add(Src2Volume.AddDisturbance(SaveData.s.BGMVolume));
        host.Tokens.Add(Src2Volume.AddDisturbance(nextSrcFadeVol));
        host.Tokens.Add(Src2Volume.AddDisturbance(host.Fader));
        
        currSrc.time = Track.StartTime;
        var src2 = nextSrc = host.Srv.gameObject.AddComponent<AudioSource>();
        src2.clip = Track.Clip;
        src2.pitch = Track.Pitch;
        host.Tokens.Add(Src2Volume.Subscribe(v => src2.volume = v));
    }

    public override void RegularUpdateSrc() {
        base.RegularUpdateSrc();
        currSrcFadeVol.Update(ETime.FRAME_TIME);
        nextSrcFadeVol.Update(ETime.FRAME_TIME);
        if (nextSrcFadeVol.Value <= 0 && nextSrc.time > Track.LoopSeconds.y)
            nextSrc.Stop();
        if (currSrc.time > Track.LoopSeconds.y && !nextSrc.isPlaying) {
            currSrcFadeVol.Push(0, -xfadeOverlap);
            nextSrcFadeVol.Push(1);
            Logs.Log($"Looping {Track.Title} at time {currSrc.time}");
            nextSrc.time = Track.LoopSeconds.x + (currSrc.time - Track.LoopSeconds.y);
            nextSrc.Play();
            (currSrc, nextSrc) = (nextSrc, currSrc);
            (currSrcFadeVol, nextSrcFadeVol) = (nextSrcFadeVol, currSrcFadeVol);
        }
    }

    public override void PauseSrc() {
        base.PauseSrc();
        nextSrc.Pause();
    }

    public override void UnPauseSrc() {
        base.UnPauseSrc();
        nextSrc.UnPause();
    }

    public override void CancelSrc() {
        base.CancelSrc();
        Src2Volume.OnCompleted();
        nextSrc.Stop();
    }
    public override void DestroySrc() {
        base.DestroySrc();
        UnityEngine.Object.Destroy(nextSrc);
    }
}
}