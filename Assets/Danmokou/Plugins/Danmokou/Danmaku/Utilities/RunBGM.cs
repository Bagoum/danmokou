using Danmokou.Core;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Utilities {
public class RunBGM : MonoBehaviour {
    public AudioTrack? bgm;

    private void Start() {
        ServiceLocator.Find<IAudioTrackService>().InvokeBGM(bgm);
    }
}
}