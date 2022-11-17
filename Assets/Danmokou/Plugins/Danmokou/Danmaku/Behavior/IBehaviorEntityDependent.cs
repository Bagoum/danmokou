namespace Danmokou.Behavior {
/// <summary>
/// A component that is dependent on a <see cref="BehaviorEntity"/> component on the same game object.
/// </summary>
public interface IBehaviorEntityDependent {
    /// <summary>
    /// Called when the behavior entity is made alive (specifically, in <see cref="BehaviorEntity.ResetValues"/>).
    /// </summary>
    void Alive();
    
    /// <summary>
    /// Called when the behavior entity is culled.
    /// </summary>
    void Died();
}
}