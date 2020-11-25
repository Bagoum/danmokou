using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;

public class AudioTrackService : MonoBehaviour {
    private static AudioTrackService main;
    [CanBeNull] private static IAudioTrackInfo bgm = null;
    
    private static readonly Dictionary<string, IAudioTrackInfo> trackInfo = new Dictionary<string, IAudioTrackInfo>();

    public AudioSource src1;
    public AudioSource src2;

    private static AudioSource currSrc;
    private static AudioSource nextSrc;

    public void Setup() {
        main = this;
        currSrc = src1;
        nextSrc = src2;

        trackInfo.Clear();
        foreach (var t in GameManagement.References.tracks) {
            if (t != null) trackInfo[t.key] = t;
        }
    }

    public void Update() {
        if (bgm != null && bgm.Loop) {
            //Debug.Log(
            //    $"{currSrc.isPlaying} {currSrc.time} {currSrc.volume};; {nextSrc.isPlaying} {nextSrc.time} {nextSrc//.volume}");
            //currentSource is the last source that was assigned to play.
            //We only send the fade request when currSrc is near its end in order to avoid desync
            if (!nextSrc.isPlaying && currSrc.time + 2 * fadeTime > bgm.LoopSeconds.y) {
                nextSrc.time = bgm.LoopSeconds.x - fadeTime;
                FadeInPlay(nextSrc, bgm, bgm.LoopSeconds.y - currSrc.time - fadeTime);
                (currSrc, nextSrc) = (nextSrc, currSrc);
            }
            if (nextSrc.time + fadeDeOverlap > bgm.LoopSeconds.y && !fading.Contains(nextSrc)) 
                StartCoroutine(sourceFadeOutStop(nextSrc, bgm, 0f));
        }
    }

    private const float fadeDeOverlap = 0.2f;
    private const float fadeTime = 0.3f;

    private static readonly HashSet<AudioSource> fading = new HashSet<AudioSource>();

    private void FadeInPlay(AudioSource src, IAudioTrackInfo over, float delay) {
        src.PlayDelayed(delay);
        StartCoroutine(_sourceFadeInPlay(src, over, delay));
    }
    
    private static IEnumerator _sourceFadeInPlay(AudioSource src, IAudioTrackInfo over, float delay) {
        if (fading.Contains(src)) yield break;
        fading.Add(src);
        float t = -delay;
        src.volume = 0f;
        for (; t < 0; t += Time.unscaledDeltaTime) {
            if (over != bgm) break;
            yield return null;
        }
        for (; t < fadeTime; t += Time.unscaledDeltaTime) {
            if (over != bgm) break;
            src.volume = over.Volume * SaveData.s.BGMVolume * (t / fadeTime);
            yield return null;
        }
        src.volume = over.Volume * SaveData.s.BGMVolume;
        
        fading.Remove(src);
    }
    private static IEnumerator sourceFadeOutStop(AudioSource src, IAudioTrackInfo over, float delay) {
        if (fading.Contains(src)) yield break;
        fading.Add(src);
        float t = -delay;
        src.volume = over.Volume * SaveData.s.BGMVolume;
        for (; t < 0; t += Time.unscaledDeltaTime) {
            if (over != bgm) break;
            yield return null;
        }
        for (; t < fadeTime; t += Time.unscaledDeltaTime) {
            if (over != bgm) break;
            src.volume = over.Volume * SaveData.s.BGMVolume * (1f - t / fadeTime);
            yield return null;
        }
        src.volume = 0f;
        src.Stop();
        fading.Remove(src);
    }

    private static void Assign(AudioSource source, IAudioTrackInfo track) {
        source.clip = track.Clip;
        source.volume = track.Volume * SaveData.s.BGMVolume;
        source.pitch = track.Pitch * pitchMult;
        source.time = track.StartTime;
    }

    public static void ReassignExistingBGMVolumeIfNotFading() {
        if (fading.Count == 0 && bgm != null) {
            main.src1.volume = bgm.Volume * SaveData.s.BGMVolume;
            main.src2.volume = bgm.Volume * SaveData.s.BGMVolume;
        }
    }

    private static void ReassignPitch() {
        if (bgm != null) {
            main.src1.pitch = bgm.Pitch * pitchMult;
            main.src2.pitch = bgm.Pitch * pitchMult;
        }
    }

    private static float pitchMult = 1f;

    public static void SetPitchMultiplier(float f) {
        pitchMult = f;
        ReassignPitch();
    }

    public static void ResetPitchMultiplier() => SetPitchMultiplier(1f);

    public static void InvokeBGM([CanBeNull] string trackName) {
        if (!string.IsNullOrWhiteSpace(trackName) && trackName != "_") 
            InvokeBGM(trackInfo.GetOrThrow(trackName, "BGM tracks"));
    }

    public static void InvokeBGM([CanBeNull] IAudioTrackInfo track) => main._InvokeBGM(track);
    
    private void _InvokeBGM([CanBeNull] IAudioTrackInfo track) {
        //Only non-BGM sounds are cancellable.
        if (track == null || bgm == track) return;
        Log.Unity($"Switching background music to: {track.Title}");
        src1.Stop();
        src2.Stop();
        bgm = track;
        Assign(src1, track);
        Assign(src2, track);
        currSrc.Play();
    }
    
    
    private DeletionMarker<Action<GameState>> gameStateListener;
    protected void OnEnable() {
        gameStateListener = Core.Events.GameStateHasChanged.Listen(HandleGameStateChange);
    }

    protected void OnDisable() {
        gameStateListener.MarkForDeletion();
        ClearAllAudio(true);
    }
    private void HandleGameStateChange(GameState state) {
        if (state.IsPaused() && (bgm?.StopOnPause ?? false)) {
            src1.Pause();
            src2.Pause();
        } else if (state == GameState.RUN && (bgm?.StopOnPause ?? true)) {
            src1.UnPause();
            src2.UnPause();
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
        ResetPitchMultiplier();
        if (force || !DoPreserveBGM) {
            main.src1.Stop();
            main.src2.Stop();
            bgm = null;
        }
    }

    public static void PreserveBGM() {
        _doPreserveBGM = true;
    }
    

}
