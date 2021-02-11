using DMK.Scriptables;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Services {
public class RunBGM : MonoBehaviour {
    public AudioTrack? bgm;

    private void Awake() {
        AudioTrackService.InvokeBGM(bgm);
    }
}
}