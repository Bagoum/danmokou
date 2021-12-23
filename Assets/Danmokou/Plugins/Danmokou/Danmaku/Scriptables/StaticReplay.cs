using System;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Scriptables {
//A replay that is saved and shipped with the game, eg. shot demo replays.
[CreateAssetMenu(menuName = "Data/Static Replay")]
public class StaticReplay : ScriptableObject {

    public TextAsset replayFile = null!;

    [NonSerialized]
    private InputManager.FrameInput[]? _frames = null;

    public Func<InputManager.FrameInput[]> Frames => () => {
        if (_frames == null || _frames.Length == 0)
            try {
                _frames = SaveData.Replays.LoadReplayFrames(replayFile)();
            } catch (Exception ex) {
                _frames = Array.Empty<InputManager.FrameInput>();
                Logs.LogException(new Exception("Failed to load static replay", ex));
            }
        return _frames;
    };

}
}