using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Services {
public enum SFXType {
    Default,
    TypingSound
}
public interface ISFXService {
    public enum SFXEventType {
        BossSpellCutin,
        BossCutin,
        BossExplode,
    }
    void Request(string? key);
    void Request(string? key, SFXType type);
    void Request(SFXConfig? sfx, SFXType type = SFXType.Default);

    void RequestSFXEvent(SFXEventType ev);


    AudioSource? RequestSource(string? key);
    AudioSource? RequestSource(SFXConfig? aci);
}
}