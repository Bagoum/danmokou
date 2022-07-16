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

    bool? Pause => null;
    bool? VNBacklogPause => null;
    bool? UIConfirm => null;
    bool? UIBack => null;
    bool? UILeft => null;
    bool? UIRight => null;
    bool? UIUp => null;
    bool? UIDown => null;
    //this needs to be separated since dialogueConfirm is replay-recordable but uiConfirm isn't
    bool ? DialogueConfirm => null;
    bool? DialogueSkipAll => null;

    void OncePerUnityFrameToggleControls();
}
}