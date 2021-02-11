using DMK.Scriptables;

namespace DMK.Services {
public interface ISFXService {
    void RequestSFX(string? key);
    void RequestSFX(SFXConfig? sfx);
}
}