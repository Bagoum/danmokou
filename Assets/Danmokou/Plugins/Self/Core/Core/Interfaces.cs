using UnityEngine;

namespace DMK.Core {

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
}