using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.Services {

public readonly struct BGMInvokeFlags {
    /// <summary>
    /// If null, existing tracks will not be destroyed.
    /// </summary>
    public readonly float? fadeOutExistingTime;
    public readonly float fadeInNewTime;

    public static BGMInvokeFlags Default => new BGMInvokeFlags(2f, 2f);
    public BGMInvokeFlags(float? fadeOutExistingTime = 2f, float fadeInNewTime = 2f) {
        this.fadeOutExistingTime = fadeOutExistingTime;
        this.fadeInNewTime = fadeInNewTime;
    }
    
}
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
        Log.Unity($"Closing audio track: {Track.Title}");
        LifetimeToken.Cancel();
        Object.Destroy(currSrc);
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
        Log.Unity($"Playing naive-loop audio track: {Track.Title}");
    }

    protected override IEnumerator _FadeIn(float time) {
        if (Active.Cancelled) yield break;
        Log.Unity($"Fading in audio track: {Track.Title}");
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
        Log.Unity($"Fading out audio track: {Track.Title}");
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
        Log.Unity($"Playing audio track: {Track.Title}");
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
            Log.Unity($"Looping {Track.Title} at time {currSrc.time}");
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
        Log.Unity($"Fading in audio track: {Track.Title}");
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
        Log.Unity($"Fading out audio track: {Track.Title}");
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
        Object.Destroy(nextSrc);
    }
}
public class AudioTrackService : CoroutineRegularUpdater {
    private static AudioTrackService main = null!;

    private static readonly Dictionary<string, IAudioTrackInfo> trackInfo = new Dictionary<string, IAudioTrackInfo>();
    private readonly DMCompactingArray<IRunningAudioTrack> tracks = new DMCompactingArray<IRunningAudioTrack>();

    public void Setup() {
        main = this;
        trackInfo.Clear();
        foreach (var t in GameManagement.References.tracks) {
            if (t != null) trackInfo[t.key] = t;
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(Events.EngineStateChanged, HandleEngineStateChange);
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii)) {
                if (!tracks[ii]._RegularUpdate())
                    tracks.Delete(ii);
            }
        }
        tracks.Compact();
    }

    public override EngineState UpdateDuring => EngineState.LOADING_PAUSE;


    public static IRunningAudioTrack? InvokeBGM(string? trackName, BGMInvokeFlags? flags = null) {
        if (!string.IsNullOrWhiteSpace(trackName) && trackName != "_")
            return InvokeBGM(trackInfo.GetOrThrow(trackName!, "BGM tracks"), flags);
        return null;
    }

    public static IRunningAudioTrack? InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags? flags = null) => 
        main._InvokeBGM(track, flags ?? BGMInvokeFlags.Default);

    private IRunningAudioTrack? _InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags flags) {
        if (track == null) return null;
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii) && !tracks[ii].Active.Cancelled && tracks[ii].Track == track)
                return tracks[ii];
        }
        if (flags.fadeOutExistingTime.Try(out var fout)) {
            for (int ii = 0; ii < tracks.Count; ++ii) {
                if (tracks.ExistsAt(ii))
                    tracks[ii].FadeOutThenDestroy(fout);
            }
        }
        IRunningAudioTrack rtrack = track.Loop switch {
            AudioTrackLoopMode.Naive => new NaiveLoopRAT(track, this),
            AudioTrackLoopMode.Timed => new TimedLoopRAT(track, this),
            _ => throw new Exception($"No handling for loop type {track.Loop} on track {track.Title}")
        };
        tracks.Add(rtrack);
        if (flags.fadeInNewTime > 0)
            rtrack.FadeIn(flags.fadeInNewTime);
        return rtrack;
    }

    protected override void OnDisable() {
        ClearAllAudio(true);
        base.OnDisable();
    }

    private void HandleEngineStateChange(EngineState state) {
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii)) {
                if (state == EngineState.RUN)
                    tracks[ii].UnPause();
                else if (tracks[ii].Track.StopOnPause)
                    tracks[ii].Pause();
            }
        }
    }

    private static bool _doPreserveBGM = false;
    private static bool DoPreserveBGM {
        get {
            if (_doPreserveBGM) {
                _doPreserveBGM = false;
                return true;
            } else return false;
        }
    }

    public static void ClearAllAudio(bool force) {
        if (force || !DoPreserveBGM) {
            for (int ii = 0; ii < main.tracks.Count; ++ii) {
                if (main.tracks.ExistsAt(ii))
                    main.tracks[ii]._Destroy();
            }
            main.tracks.Empty();
        }
    }

    public static void PreserveBGM() {
        _doPreserveBGM = true;
    }
}
}
