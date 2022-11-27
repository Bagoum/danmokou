namespace Danmokou.UI.XML {
public record UITransitionOptions {
    public bool Animate { get; init; } = true;
    public float ScreenTransitionTime { get; init; } = 0.4f;
    public bool DelayScreenFadeIn { get; init; } = true;


    public static readonly UITransitionOptions Default = new();
    public static readonly UITransitionOptions DontAnimate = new() { Animate = false };
}
}