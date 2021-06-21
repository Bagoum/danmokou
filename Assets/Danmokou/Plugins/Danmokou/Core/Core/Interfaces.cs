using BagoumLib.Culture;
using UnityEngine;

namespace Danmokou.Core {
public enum AudioTrackLoopMode {
    None,
    Naive,
    Timed
}
public interface IAudioTrackInfo {
    /// <summary>
    /// eg. "Mima's theme" or "Stage 3 theme"
    /// </summary>
    string TrackPlayLocation { get; }
    /// <summary>
    /// eg. "Moonlit Flamenco"
    /// </summary>
    string Title { get; }
    AudioClip Clip { get; }
    float Volume { get; }
    float Pitch { get; }
    bool StopOnPause { get; }
    AudioTrackLoopMode Loop { get; }
    Vector2 LoopSeconds { get; }
    float StartTime { get; }
    
    /// <summary>
    /// True to show and make playable.
    /// False to show a non-playable ??? in this space.
    /// Null to skip this entry.
    /// </summary>
    bool? DisplayInMusicRoom { get; }
    
    LString MusicRoomDescription { get; }
    
}
}