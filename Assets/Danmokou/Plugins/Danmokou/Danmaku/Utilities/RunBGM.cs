using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Utilities {
public class RunBGM : MonoBehaviour {
    public AudioTrack? bgm;

    private void Awake() {
        AudioTrackService.InvokeBGM(bgm);
    }
}
}