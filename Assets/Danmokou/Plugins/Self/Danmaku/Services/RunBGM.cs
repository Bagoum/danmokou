using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {

public class RunBGM : MonoBehaviour {
    [CanBeNull] public AudioTrack bgm;

    private void Awake() {
        AudioTrackService.InvokeBGM(bgm);
    }
}
}