namespace Danmokou.Core.DInput {

/// <summary>
/// An abstraction representing a primary input source (eg. KBM, controller, or touch input).
/// </summary>
public interface IPrimaryInputSource : IDescriptiveInputSource {
    public MainInputSource Container { set; }
}

/// <summary>
/// An abstraction enclosing multiple <see cref="IPrimaryInputSource"/>s,
///  only one of which may be "active" at a given time.
/// <br/>This allows customizing button tooltips and the like to the "currently active input mechanism".
/// </summary>
public class MainInputSource {
    private readonly IPrimaryInputSource[] sources;
    public IPrimaryInputSource Current { get; private set; }

    public MainInputSource(params IPrimaryInputSource[] sources) {
        this.sources = sources;
        foreach (var s in sources)
            s.Container = this;
        Current = sources[0];
    }

    private IPrimaryInputSource? nextCurrent = null;
    public void MarkActive(IPrimaryInputSource src) {
        nextCurrent ??= src;
    }

    public void OncePerUnityFrameToggleControls() {
        nextCurrent = null;
        for (int ii = 0; ii < sources.Length; ++ii)
            sources[ii].OncePerUnityFrameToggleControls();
        Current = nextCurrent ?? Current;
        //Logs.Log($"Current method: {Current}, updated {nextCurrent}");
    }
}
}