using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {

public class RunBGM : MonoBehaviour {
    [CanBeNull] public AudioTrack bgm;

    private void Start() {
        AudioTrackService.InvokeBGM(bgm);
    }
}
}