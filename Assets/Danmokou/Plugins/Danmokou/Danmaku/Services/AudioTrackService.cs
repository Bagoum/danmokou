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
using Danmokou.GameInstance;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.Services {

public readonly struct BGMInvokeFlags {
    public readonly float fadeOutExistingTime;
    public readonly float fadeInNewTime;
    public static BGMInvokeFlags Default => new(2f, 2f);
    public BGMInvokeFlags(float fadeOutExistingTime = 2f, float fadeInNewTime = 2f) {
        this.fadeOutExistingTime = fadeOutExistingTime;
        this.fadeInNewTime = fadeInNewTime;
    }
    
}

public interface IAudioTrackService {
    void ClearRunningBGM(BGMInvokeFlags? flags = null);
    IRunningAudioTrack? InvokeBGM(string? trackName, BGMInvokeFlags? flags = null, ICancellee? cT = null);
    IRunningAudioTrack? InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags? flags = null, ICancellee? cT = null);
}
public class AudioTrackService : CoroutineRegularUpdater, IAudioTrackService {
    private static readonly Dictionary<string, IAudioTrackInfo> trackInfo = new();
    //Many tracks may run simultaneously
    private readonly DMCompactingArray<IRunningAudioTrack> tracks = new();
    //Only one BGM may run at a time, the rest are paused
    // All bgm are included in tracks
    private readonly LinkedList<IRunningAudioTrack> bgm = new();

    private bool preserveBGMOnNextScene = false;

    public void Setup() {
        trackInfo.Clear();
        foreach (var t in GameManagement.References.tracks) {
            if (t != null) trackInfo[t.key] = t;
        }
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IAudioTrackService>(this);

        Listen(Events.SceneCleared, () => {
            if (preserveBGMOnNextScene)
                preserveBGMOnNextScene = false;
            else
                ClearAllAudio();
        });
        Listen(EngineStateManager.EvState, HandleEngineStateChange);
        Listen(InstanceRequest.InstanceRestarted, ir => {
            if (ir.Mode.PreserveReloadAudio())
                preserveBGMOnNextScene = true;
        });
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
    

    public IRunningAudioTrack? InvokeBGM(string? trackName, BGMInvokeFlags? flags = null, ICancellee? cT = null) {
        if (!string.IsNullOrWhiteSpace(trackName) && trackName != "_")
            return InvokeBGM(trackInfo.GetOrThrow(trackName!, "BGM tracks"), flags, cT);
        return null;
    }

    public void ClearRunningBGM(BGMInvokeFlags? flags = null) {
        var f = flags ?? BGMInvokeFlags.Default;
        for (var n = bgm.First; n != null; n = n.Next)
            n.Value.FadeOut(f.fadeOutExistingTime, AudioTrackState.DestroyReady);
        bgm.Clear();
    }
    public IRunningAudioTrack? InvokeBGM(IAudioTrackInfo? track, BGMInvokeFlags? flags = null, ICancellee? cT = null) {
        if (track == null) return null;
        IRunningAudioTrack rtrack;
        for (var n = bgm.Last; n != null; n = n.Previous) {
            if (n.Value is { } t && t.State <= AudioTrackState.Paused && t.Track == track) {
                //increase priority
                //TODO update cT?
                if (n != bgm.Last) {
                    bgm.Remove(n);
                    bgm.AddLast(n);
                }
                if (t.State > AudioTrackState.Active)
                    t.UnPause();
                rtrack = t;
                goto play_track;
            }
        }
        rtrack = track.Loop switch {
            AudioTrackLoopMode.Naive => new NaiveLoopRAT(track, this, cT) { IsRunningAsBGM = true },
            AudioTrackLoopMode.Timed => new TimedLoopRAT(track, this, cT) { IsRunningAsBGM = true },
            _ => throw new Exception($"No handling for loop type {track.Loop} on track {track.Title}")
        };
        rtrack.AddScopedToken(tracks.Add(rtrack));
        var rnode = bgm.AddLast(rtrack);
        rtrack.AddScopedToken(rtrack.State.Subscribe(s => RecheckTracks(rnode, s)));
        rtrack.AddScopedToken(new JointDisposable(() => {
            //If ClearRunningBGM was used, then rnode will already have been removed
            if (rnode.List == bgm)
                bgm.Remove(rnode);
        }));
        play_track: ;
        var f = flags ?? BGMInvokeFlags.Default;
        if (f.fadeInNewTime > 0)
            rtrack.FadeIn(f.fadeInNewTime);
        return rtrack;
    }

    private void RecheckTracks(LinkedListNode<IRunningAudioTrack> changed, AudioTrackState toState) {
        //If we are changing a track to active, then move it above the current playing track (if it exists) and play it,
        // and pause all tracks below.
        //If we are changing a track to any other state, then start playing the next track below it,
         // as long as there is no active BGM at a higher priority.
        //Note that even if toState=DestroyReady, that doesn't mean it's been destroyed yet. It still exists in bgm
        if (toState == AudioTrackState.Active) {
            for (var n = bgm.Last; n != changed; n = n.Previous) {
                if (n!.Value.State == AudioTrackState.Active) {
                    //move changed above n and pause n
                    bgm.Remove(changed);
                    bgm.AddAfter(n, changed);
                    break;
                }
            }
            for (var n = changed.Previous; n != null; n = n.Previous) {
                if (n.Value.State == AudioTrackState.Active)
                    //TODO carry fade configuration in the AudioTrackState (as a record type) and read it from toState
                    n.Value.FadeOut(2, AudioTrackState.Paused);
            }
        } else {
            for (var n = bgm.Last; n != changed && n != null; n = n.Previous) {
                //Something is already playing at a higher priority, no issue
                if (n!.Value.State == AudioTrackState.Active)
                    return;
            }
            for (var n = changed.Previous; n != null; n = n.Previous) {
                if (n.Value.State.Value is AudioTrackState.Pausing or AudioTrackState.Paused) {
                    n.Value.UnPause();
                    n.Value.FadeIn(2);
                }
            }
        }
    }

    protected override void OnDisable() {
        ClearAllAudio();
        base.OnDisable();
    }

    private void HandleEngineStateChange(EngineState state) {
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.GetIfExistsAt(ii, out var t)) {
                //Handle bgm separately
                if (t.IsRunningAsBGM) continue;
                if (state == EngineState.RUN && t.State.Value is AudioTrackState.Pausing or AudioTrackState.Paused)
                    tracks[ii].UnPause();
                else if (t.State.Value is AudioTrackState.Active && t.Track.StopOnPause)
                    tracks[ii].Pause();
            }
        }
        //TODO handle menu-pause for BGM
    }

    public void ClearAllAudio() {
        for (int ii = 0; ii < tracks.Count; ++ii) {
            if (tracks.ExistsAt(ii))
                tracks[ii].CancelAndDestroy();
        }
        tracks.Empty();
    }
}
}
