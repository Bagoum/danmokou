using Danmokou.Danmaku.Options;

namespace Danmokou.Behavior {
/// <summary>
/// A component that is dependent on a <see cref="BehaviorEntity"/> component on the same game object.
/// </summary>
public interface IBehaviorEntityDependent {
    /// <summary>
    /// Called when the behavior entity is made alive (specifically, in <see cref="BehaviorEntity.ResetValues"/>).
    /// </summary>
    void Alive() { }

    /// <summary>
    /// Called if/when the behavior entity receives an Initialize call.
    /// <br/>This is generally only called for entities created through scripting code.
    /// </summary>
    void Initialized(RealizedBehOptions? options) { }

    /// <summary>
    /// Called when the behavior entity is culled.
    /// </summary>
    void Died() { }
}
}