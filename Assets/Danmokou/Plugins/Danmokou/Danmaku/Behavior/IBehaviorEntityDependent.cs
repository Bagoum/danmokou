using System;
using Danmokou.Danmaku.Options;
using UnityEngine;

namespace Danmokou.Behavior {
/// <summary>
/// A component that is dependent on a <see cref="BehaviorEntity"/> component on the same game object.
/// </summary>
public interface IBehaviorEntityDependent {
    /// <summary>
    /// Functionality that should be called in Awake on first initialization and ResetValues on later initialization.
    /// </summary>
    void OnLinkOrResetValues(bool isLink);
    
    /// <summary>
    /// Called if/when the behavior entity receives an Initialize call.
    /// <br/>This is generally only called for entities created through scripting code.
    /// <br/>This will occur after <see cref="OnLinkOrResetValues"/>.
    /// </summary>
    void Initialized(RealizedBehOptions? options) { }

    /// <summary>
    /// Called from <see cref="BehaviorEntity.UpdateRendering"/>, at the end of RegularUpdate.
    /// </summary>
    /// <param name="isFirstFrame">True if this is the first frame update for the object.
    /// It is sometimes efficient to skip rendering updates when !ETime.LastUpdateForScreen,
    /// but rendering updates should not be skipped on the first frame.</param>
    /// <param name="lastDesiredDelta">The last delta describing the entity's attempted movement.</param>
    void OnRender(bool isFirstFrame, Vector2 lastDesiredDelta) { }
    
    /// <summary>
    /// Called when the entity's style changes.
    /// </summary>
    void StyleChanged(BehaviorEntity.StyleMetadata style) { }

    /// <summary>
    /// Called when the entity is destroyed.
    /// </summary>
    void Culled(bool allowFinalize, Action done);
}


}