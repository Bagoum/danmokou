using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public static BGMInvokeFlags Default { get; } = new(2f, 2f);
    public BGMInvokeFlags(float fadeOutExistingTime = 2f, float fadeInNewTime = 2f) {
        this.fadeOutExistingTime = fadeOutExistingTime;
        this.fadeInNewTime = fadeInNewTime;
    }
    
}

public interface IAudioTrackService {
    void ClearRunningBGM(BGMInvokeFlags? flags = null);
    IAudioTrackInfo? FindTrack(string? trackName);
    
    /// <inheritdoc cref="FindTrackset(System.Collections.Generic.IEnumerable{string?})"/>
    AudioTrackSet? FindTrackset(IEnumerable<string?> tracks) =>
        FindTrackset(tracks.Select(FindTrack).ToArray());
    
    /// <summary>
    /// Find an existing trackset which has all the provided tracks.
    /// </summary>
    AudioTrackSet? FindTrackset(IAudioTrackInfo?[] tracks);
    AudioTrackSet AddTrackset(BGMInvokeFlags? flags = null, PIData? pi = null, ICancellee? cT = null);
}
public class AudioTrackService : CoroutineRegularUpdater, IAudioTrackService {
    private static readonly Dictionary<string, IAudioTrackInfo> trackInfo = new();
    //Many tracks may run simultaneously
    private readonly DMCompactingArray<AudioTrackSet> tracks = new();
    //Only one BGM may run at a time, the rest are paused
    // All bgm are included in tracks
    private readonly LinkedList<AudioTrackSet> bgm = new();

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

    public void ClearRunningBGM(BGMInvokeFlags? flags = null) {
        for (var n = bgm.First; n != null; n = n.Next)
            n.Value.FadeOut(flags?.fadeOutExistingTime, AudioTrackState.DestroyReady);
        bgm.Clear();
    }

    public IAudioTrackInfo? FindTrack(string? trackName) {
        if (!string.IsNullOrWhiteSpace(trackName) && trackName != "_")
            return AudioTrackService.trackInfo.GetOrThrow(trackName!, "BGM tracks");
        return null;
    }

    public AudioTrackSet? FindTrackset(IAudioTrackInfo?[] tracks) {
        for (var n = bgm.Last; n != null; n = n.Previous) {
            for (int ii = 0; ii < tracks.Length; ++ii)
                if (!n.Value.HasTrack(tracks[ii], out _))
                    goto next;
            return n.Value;
            next: ;
        }
        return null;
    }

    public AudioTrackSet AddTrackset(BGMInvokeFlags? flags = null, PIData? pi = null, ICancellee? cT = null) {
        var trackset = new AudioTrackSet(this, flags, pi, cT);
        trackset.Tokens.Add(tracks.Add(trackset));
        var rnode = bgm.AddLast(trackset);
        trackset.Tokens.Add(trackset.State.Subscribe(s => RecheckTracks(rnode, s)));
        trackset.Tokens.Add(new JointDisposable(() => {
            //If ClearRunningBGM was used, then rnode will already have been removed
            if (rnode.List == bgm)
                bgm.Remove(rnode);
        }));
        return trackset;
    }
    private void RecheckTracks(LinkedListNode<AudioTrackSet> changed, AudioTrackState toState) {
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
                    n.Value.FadeOut(changed.Value.Flags?.fadeOutExistingTime, AudioTrackState.Paused);
            }
        } else {
            for (var n = bgm.Last; n != changed && n != null; n = n.Previous) {
                //Something is already playing at a higher priority, no issue
                if (n!.Value.State == AudioTrackState.Active)
                    goto end;
            }
            for (var n = changed.Previous; n != null; n = n.Previous) {
                if (n.Value.State.Value is AudioTrackState.Pausing or AudioTrackState.Paused) {
                    n.Value.UnPause();
                    n.Value.FadeIn(null);
                }
            }
        }
        end: ;
        var sb = new StringBuilder();
        sb.Append("Track state updated. Current state:");
        for (var n = bgm.Last; n != null; n = n.Previous) {
            sb.Append($"\n{n.Value.State.Value}: {n.Value.TrackNames}");
        }
        Logs.Log(sb.ToString());
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
                //else if (t.State.Value is AudioTrackState.Active && t.Track.StopOnPause)
                //    tracks[ii].Pause();
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

    [ContextMenu("Pause topmost")]
    public void PauseTopmostAudio() {
        bgm.Last.Value.FadeOut(1, AudioTrackState.Paused);
    }
}
}
