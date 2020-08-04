using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;

public interface IAudioTrackInfo {
    string Title { get; }
    AudioClip Clip { get; }
    float Volume { get; }
    float Pitch { get; }
    bool StopOnPause { get; }
    bool Loop { get; }
    Vector2 LoopSeconds { get; }
    float StartTime { get; }
}

[CreateAssetMenu(menuName = "AudioTrack")]
public class AudioTrack : ScriptableObject, IAudioTrackInfo {
    public AudioClip clip;

    public string key;
    public string title;
    public float volume = .2f;
    public float pitch = 1f;
    public bool stopOnPause;
    public bool loop;

    public Vector2 loopSeconds;
    public float startWithTime = 0f;

    public AudioClip Clip => clip;
    public float Volume => volume;
    public float Pitch => pitch;
    public bool StopOnPause => stopOnPause;
    public bool Loop => loop;
    public Vector2 LoopSeconds => loopSeconds;
    public float StartTime => startWithTime;
    public string Title => title;
}