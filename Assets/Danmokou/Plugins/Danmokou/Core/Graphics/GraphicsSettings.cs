using BagoumLib.Events;

namespace Danmokou.Graphics {
public interface IGraphicsSettings {
    (int w, int h) Resolution { get; }
    bool Shaders { get; }

    public static readonly ReplayEvent<IGraphicsSettings> SettingsEv = new(1);
}
}