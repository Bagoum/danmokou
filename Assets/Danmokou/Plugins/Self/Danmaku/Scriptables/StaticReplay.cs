using System;
using DMK.Core;
using UnityEngine;

namespace DMK.Scriptables {
//A replay that is saved and shipped with the game, eg. shot demo replays.
[CreateAssetMenu(menuName = "Data/Static Replay")]
public class StaticReplay : ScriptableObject {

    public TextAsset replayFile = null!;

    [NonSerialized]
    private InputManager.FrameInput[]? _frames = null;

    public Func<InputManager.FrameInput[]> Frames => () => {
        if (_frames == null || _frames.Length == 0)
            _frames = SaveData.Replays.LoadReplayFrames(replayFile)();
        return _frames;
    };

}
}