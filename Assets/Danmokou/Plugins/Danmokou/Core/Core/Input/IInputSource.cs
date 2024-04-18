using System;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using UnityEngine;

namespace Danmokou.Core.DInput {
/// <summary>
/// A method of input that provides some subset of input information.
/// <br/>KBM, controller, or mobile touch input are primary input sources
///  that should provide almost all of these values.
/// <br/>Replays and external code-directed input are secondary input sources
///  that provide only a few of these values.
/// </summary>
public interface IInputSource {
    public const short minSpeed = -short.MaxValue;
    public const short maxSpeed = short.MaxValue;
    short? HorizontalSpeed => null;
    short? VerticalSpeed => null;
    bool? Firing => null;
    bool? Focus => null;
    bool? Bomb => null;
    bool? Meter => null;
    bool? Swap => null;
    bool? Fly => null;
    bool? SlowFall => null;

    bool? Pause => null;
    bool? VNBacklogPause => null;
    bool? UIConfirm => null;
    bool? UIBack => null;
    bool? UIContextMenu => null;
    bool? UILeft => null;
    bool? UIRight => null;
    bool? UIUp => null;
    bool? UIDown => null;
    //this needs to be separated since dialogueConfirm is replay-recordable but uiConfirm isn't
    // (we can't record uiConfirm as we generally don't want to record pauses and the like,
    // but in a more generalized game, it may be necessary to support unification)
    bool ? DialogueConfirm => null;
    bool? DialogueSkipAll => null;

    /// <summary>
    /// Update all controls (called once per Unity frame).
    /// </summary>
    /// <returns>True iff any input was pressed this frame.</returns>
    bool OncePerUnityFrameToggleControls();

    /// <inheritdoc cref="IInputHandler.Interrupt"/>
    void Interrupt();
}

/// <summary>
/// An input source that returns false for all inputs.
/// </summary>
public class NullInputSource : IInputSource {
    public short? HorizontalSpeed => 0;
    public short? VerticalSpeed => 0;
    public bool? Firing => false;
    public bool? Focus => false;
    public bool? Bomb => false;
    public bool? Meter => false;
    public bool? Swap => false;
    public bool? Fly => false;
    public bool? SlowFall => false;

    public bool? Pause => false;
    public bool? VNBacklogPause => false;
    public bool? UIConfirm => false;
    public bool? UIBack => false;
    public bool? UIContextMenu => false;
    public bool? UILeft => false;
    public bool? UIRight => false;
    public bool? UIUp => false;
    public bool? UIDown => false;
    public bool ? DialogueConfirm => false;
    public bool? DialogueSkipAll => false;
    public bool OncePerUnityFrameToggleControls() {
        return false;
    }
    public void Interrupt() { }
}

}