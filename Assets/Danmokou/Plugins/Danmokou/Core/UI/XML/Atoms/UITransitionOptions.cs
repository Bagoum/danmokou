namespace Danmokou.UI.XML {
public record UITransitionOptions {
    public bool Animate { get; init; } = true;
    public float ScreenTransitionTime { get; init; } = 0.4f;

    /// <summary>
    /// Amount of time by which to delay the fade-in of the next screen, as a *ratio* of
    /// <see cref="ScreenTransitionTime"/>.
    /// <br/>The total transition time is ScreenTransitionTime * (1+DelayScreenFadeInRatio).
    /// </summary>
    public float DelayScreenFadeInRatio { get; init; } = 0.5f;


    public static readonly UITransitionOptions Default = new();
    public static readonly UITransitionOptions DontAnimate = new() { Animate = false };
}
}