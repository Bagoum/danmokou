using DMK.Scriptables;
using UnityEngine;

namespace DMK.Services {
public interface ISFXService {
    public enum SFXEventType {
        BossSpellCutin,
        BossCutin,
        BossExplode,
    }
    void Request(string? key);
    void Request(SFXConfig? sfx);

    void RequestSFXEvent(SFXEventType ev);

    AudioSource? RequestSource(SFXConfig? aci);
}
}