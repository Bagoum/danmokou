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

public static class InputHelpers {
    public static InputChecker Key(KeyCode key) =>
        new InputChecker(() => Input.GetKey(key), new LText(key.ToString()));
    public static IInputHandler TKey(KeyCode key) => InputHandler.Trigger(Key(key));
    public static InputChecker AxisL0(string axis) =>
        new InputChecker(() => Input.GetAxisRaw(axis) < -0.1f, new LText(axis));

    public static InputChecker AxisG0(string axis) =>
        new InputChecker(() => Input.GetAxisRaw(axis) > 0.1f, new LText(axis));
    public static float GetAxisRawC(string key) => Input.GetAxisRaw(key);
    public static bool AxisIsActive(string key) => Mathf.Abs(Input.GetAxisRaw(key)) > 0.1f;
    
    public static InputChecker Mouse(int key) =>
        new InputChecker(() => Input.GetMouseButton(key), new LText(key.ToString()));
}
}