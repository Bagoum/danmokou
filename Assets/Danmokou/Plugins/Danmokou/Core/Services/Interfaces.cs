using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Services {
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