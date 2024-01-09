using System;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using Danmokou.Danmaku;
using Danmokou.Player;

namespace Danmokou.GameInstance {
/// <summary>
/// Contains information about a mechanic (eg. the faith meter) that can be slotted in or out of
///  an executing danmaku game in <see cref="InstanceData"/>.
/// </summary>
public interface IInstanceFeature : IDisposable {
    /// <summary>
    /// Called when the player loses a life (takes a hit).
    /// </summary>
    void OnDied() { }
    
    /// <summary>
    /// Called when the player loses all their lives and selects "continue" or "restart from checkpoint".
    /// </summary>
    void OnContinueOrCheckpoint() { }

    /// <summary>
    /// Called every frame while the player is active (ie. not under control restrictions).
    /// </summary>
    /// <param name="lenient">If true, then time-based functionality (such as faith or combo) should not run.</param>
    /// <param name="state">Current state of the player.</param>
    void OnPlayerFrame(bool lenient, PlayerController.PlayerState state) { }

    /// <summary>
    /// Called on GameManagement regular update.
    /// </summary>
    void OnRegularUpdate() { }

    /// <summary>
    /// Add a lenience period for time-based mechanics.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    void AddLenience(double time) { }
    
    /// <summary>
    /// Called when the score changes.
    /// </summary>
    /// <param name="score">The new score.</param>
    void OnScoreChanged(long score) { }

    /// <summary>
    /// Called when the player grazes.
    /// </summary>
    void OnGraze(int delta) { }

    #region ItemMethods
    //Note: these methods are for "side effect" handling, eg. when faith increases because a value item was picked up. When you are collecting an item that is specific to a feature (eg. power item for power feature, value item for score feature), make a specialized method for that.
    //Also, if you have an event on InstanceData that sends out these messages, then instead of making a method, just subscribe to the event in the constructor.
    void OnItemPower(int delta) { }
    void OnItemFullPower(int delta) { }
    void OnItemValue(int delta, double multiplier) { }
    void OnItemSmallValue(int delta, double multiplier) { }
    void OnItemPointPP(int delta, double multiplier) { }
    void OnItemLife(int delta) { }
    void OnItemGem(int delta) { }
    void OnItemOneUp() { }
    
    #endregion
    
    void OnPhaseEnd(in PhaseCompletion pc, in CardRecord? crec) { }

    void OnEnemyDestroyed() { }
    
}

public abstract class BaseInstanceFeature : IInstanceFeature, ITokenized {
    public List<IDisposable> Tokens { get; } = new();
}

/// <summary>
/// Degenerate object to create an instance feature given a reference to the containing instance.
/// <br/>This can be done more efficiently when static interface methods are supported in Unity.
/// </summary>
public interface IFeatureCreator<T> where T : IInstanceFeature {
    public T Create(InstanceData instance);
}

}