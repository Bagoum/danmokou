using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using UnityEngine;
using Object = System.Object;

namespace Danmokou.Services {
public interface IRunningAudioTrack {
    IAudioTrackInfo Track { get; }
    bool _RegularUpdate();
    void FadeIn(float time);
    void FadeOutThenDestroy(float time);

    void Pause();
    void UnPause();
    
    ICancellee Active { get; }
    
    /// <summary>
    /// Destroy the track's resources on the next update.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Used to instantly destroy the track's resources.
    /// </summary>
    void _Destroy();
}

public abstract class BaseRunningAudioTrack : IRunningAudioTrack {
    public IAudioTrackInfo Track { get; }
    public Cancellable LifetimeToken { get; } = new Cancellable();
    public ICancellee Active { get; }

    protected readonly AudioTrackService host;
    protected readonly List<IDisposable> tokens = new List<IDisposable>();
    protected DisturbedProduct<float> Src1Volume { get; }
    protected AudioSource currSrc;

    public BaseRunningAudioTrack(IAudioTrackInfo track, AudioTrackService host, ICancellee? cT = null) {
        Track = track;
        Active = new JointCancellee(cT ?? Cancellable.Null, LifetimeToken);
        this.host = host;
        var src1 = currSrc = host.gameObject.AddComponent<AudioSource>();
        Src1Volume = new DisturbedProduct<float>(track.Volume);
        tokens.Add(Src1Volume.AddDisturbance(SaveData.s.BGMVolumeEv));
        tokens.Add(Src1Volume.Subscribe(v => src1.volume = v));
        src1.clip = track.Clip;
        src1.pitch = track.Pitch;
    }

    protected bool? __RegularUpdate() {
        if (Active.Cancelled) {
            Destroy();
            return false;
        }
        if (EngineStateManager.State != EngineState.RUN && Track.StopOnPause)
            return true;
        return null;
    }

    public virtual bool _RegularUpdate() => __RegularUpdate() ?? true;
    
    
    public void FadeIn(float time) => 
        host.Run(_FadeIn(time), flags: new CoroutineOptions(true, CoroutineType.StepTryPrepend));

    protected abstract IEnumerator _FadeIn(float time);
    
    public void FadeOutThenDestroy(float time) => host.RunDroppableRIEnumerator(_FadeOutThenDestroy(time));
    protected abstract IEnumerator _FadeOutThenDestroy(float time);
    
    public virtual void Pause() {
        currSrc.Pause();
    }
    public virtual void UnPause() {
        currSrc.UnPause();
    }
    
    public virtual void Cancel() {
        currSrc.Stop();
        LifetimeToken.Cancel();
    }
    
    protected virtual void Destroy() {
        Logs.Log($"Closing audio track: {Track.Title}");
        LifetimeToken.Cancel();
        UnityEngine.Object.Destroy(currSrc);
        foreach (var t in tokens)
            t.Dispose();
    }

    public void _Destroy() => Destroy();
}
public class NaiveLoopRAT : BaseRunningAudioTrack {
    public NaiveLoopRAT(IAudioTrackInfo track, AudioTrackService host, ICancellee? cT = null) : base(track, host, cT) {
        currSrc.loop = true;
        currSrc.time = track.StartTime;
        currSrc.Play();
        Logs.Log($"Playing naive-loop audio track: {Track.Title}");
    }

    protected override IEnumerator _FadeIn(float time) {
        if (Active.Cancelled) yield break;
        Logs.Log($"Fading in audio track: {Track.Title}");
        var fader = new PushLerper<float>(time, Mathf.Lerp);
        fader.Push(0);
        fader.Push(1);
        var t1 = Src1Volume.AddDisturbance(fader);
        tokens.Add(t1);
        while (fader.Value < 1) {
            fader.Update(ETime.FRAME_TIME);
            yield return null;
            if (Active.Cancelled) yield break;
        }
        t1.Dispose();
    }
    
    protected override IEnumerator _FadeOutThenDestroy(float time) {
        if (Active.Cancelled) yield break;
        Logs.Log($"Fading out audio track: {Track.Title}");
        var fader = new PushLerper<float>(time, Mathf.Lerp);
        fader.Push(1);
        fader.Push(0);
        tokens.Add(Src1Volume.AddDisturbance(fader));
        while (fader.Value > 0) {
            fader.Update(ETime.FRAME_TIME);
            yield return null;
            if (Active.Cancelled) yield break;
        }
        LifetimeToken.Cancel();
    }
}
public class TimedLoopRAT : BaseRunningAudioTrack {
    private const float xfadeTime = 0.3f;
    private const float xfadeOverlap = 0.2f;
    //Two sources are necessary to smoothly loop by crossfading the track into itself.
    private PushLerper<float> currSrcFadeVol = new PushLerper<float>(xfadeTime, Mathf.Lerp);
    private PushLerper<float> nextSrcFadeVol = new PushLerper<float>(xfadeTime, Mathf.Lerp);
    private DisturbedProduct<float> Src2Volume { get; }
    private AudioSource nextSrc;

    public TimedLoopRAT(IAudioTrackInfo track, AudioTrackService host, ICancellee? cT = null) : base(track, host, cT) {
        var src1 = currSrc;
        currSrcFadeVol.Push(1);
        tokens.Add(Src1Volume.AddDisturbance(currSrcFadeVol));
        var src2 = nextSrc = host.gameObject.AddComponent<AudioSource>();
        nextSrcFadeVol.Push(0);
        Src2Volume = new DisturbedProduct<float>(track.Volume);
        tokens.Add(Src2Volume.AddDisturbance(SaveData.s.BGMVolumeEv));
        tokens.Add(Src2Volume.AddDisturbance(nextSrcFadeVol));
        tokens.Add(Src2Volume.Subscribe(v => src2.volume = v));

        src1.clip = src2.clip = track.Clip;
        src1.pitch = src2.pitch = track.Pitch;
        
        currSrc.time = track.StartTime;
        currSrc.Play();
        Logs.Log($"Playing audio track: {Track.Title}");
    }

    /// <summary>
    /// Returns false iff this object should be removed (Destroy will already have been run).
    /// </summary>
    /// <returns></returns>
    public override bool _RegularUpdate() {
        if (__RegularUpdate().Try(out var b)) return b;
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
        return true;
    }

    public override void Pause() {
        currSrc.Pause();
        nextSrc.Pause();
    }

    public override void UnPause() {
        currSrc.UnPause();
        nextSrc.UnPause();
    }

    protected override IEnumerator _FadeIn(float time) {
        if (Active.Cancelled) yield break;
        Logs.Log($"Fading in audio track: {Track.Title}");
        var fader = new PushLerper<float>(time, Mathf.Lerp);
        fader.Push(0);
        fader.Push(1);
        var t1 = Src1Volume.AddDisturbance(fader);
        tokens.Add(t1);
        var t2 = Src2Volume.AddDisturbance(fader);
        tokens.Add(t2);
        while (fader.Value < 1) {
            fader.Update(ETime.FRAME_TIME);
            yield return null;
            if (Active.Cancelled) yield break;
        }
        t1.Dispose();
        t2.Dispose();
    }
    
    protected override IEnumerator _FadeOutThenDestroy(float time) {
        if (Active.Cancelled) yield break;
        Logs.Log($"Fading out audio track: {Track.Title}");
        var fader = new PushLerper<float>(time, Mathf.Lerp);
        fader.Push(1);
        fader.Push(0);
        tokens.Add(Src1Volume.AddDisturbance(fader));
        tokens.Add(Src2Volume.AddDisturbance(fader));
        while (fader.Value > 0) {
            fader.Update(ETime.FRAME_TIME);
            yield return null;
            if (Active.Cancelled) yield break;
        }
        LifetimeToken.Cancel();
    }

    public override void Cancel() {
        base.Cancel();
        nextSrc.Stop();
    }
    protected override void Destroy() {
        base.Destroy();
        UnityEngine.Object.Destroy(nextSrc);
    }
}
}