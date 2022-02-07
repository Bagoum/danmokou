using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using Danmokou.DMath;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static Danmokou.Core.DInput.InputHelpers;

namespace Danmokou.Core.DInput {
public class ControllerInputSource : IKeyedInputSource, IPrimaryInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    public MainInputSource Container { get; set; } = null!;
    public ControllerInputSource() {
        ((IKeyedInputSource)this).AddUpdaters();
    }
    
    private const string aCHoriz = "Horizontal";
    private const string aCVert = "Vertical";
    private const string aCRightX = "ControllerRightX";
    private const string aCRightY = "ControllerRightY";
    private const string aCDPadX = "DPadX";
    private const string aCDPadY = "DPadY";
    private const string aCLeftTrigger = "ControllerLTrigger";
    private const string aCRightTrigger = "ControllerRTrigger";

    private const KC cLeftShoulder = KC.JoystickButton4;
    private const KC cRightShoulder = KC.JoystickButton5;
    private const KC cA = KC.JoystickButton0;
    private const KC cB = KC.JoystickButton1;
    private const KC cX = KC.JoystickButton2;
    private const KC cY = KC.JoystickButton3;
    private const KC cSelect = KC.JoystickButton6;
    private const KC cStart = KC.JoystickButton7;

    public IInputHandler arrowLeft { get; } = InputHandler.Trigger(AxisL0(aCDPadX));
    public IInputHandler arrowRight { get; } = InputHandler.Trigger(AxisG0(aCDPadX));
    public IInputHandler arrowUp { get; } = InputHandler.Trigger(AxisG0(aCDPadY));
    public IInputHandler arrowDown { get; } = InputHandler.Trigger(AxisL0(aCDPadY));
    public IInputHandler focusHold { get; } = InputHandler.Hold(AxisG0(aCRightTrigger));
    public IInputHandler fireHold { get; } = InputHandler.Hold(AxisG0(aCLeftTrigger));
    public IInputHandler bomb { get; } = TKey(cX);
    public IInputHandler meter { get; } = TKey(cX);
    public IInputHandler swap { get; } = TKey(cY);
    public IInputHandler pause { get; } = TKey(cStart);
    public IInputHandler vnBacklogPause { get; } = TKey(cSelect);
    public IInputHandler uiConfirm { get; } = TKey(cA);
    public IInputHandler uiBack { get; } = TKey(cB);
    public IInputHandler? dialogueSkipAll { get; } = null;
    
    public short? HorizontalSpeed =>
        M.ClampS(IInputSource.minSpeed, IInputSource.maxSpeed, 
            (short)(IInputSource.maxSpeed * Math.Clamp(GetAxisRawC(aCHoriz) + GetAxisRawC(aCDPadX), -1, 1)));
    public short? VerticalSpeed =>
        M.ClampS(IInputSource.minSpeed, IInputSource.maxSpeed, 
            (short)(IInputSource.maxSpeed * Math.Clamp(GetAxisRawC(aCVert) + GetAxisRawC(aCDPadY), -1, 1)));
    
    void IInputSource.OncePerUnityFrameToggleControls() {
        if (((IInputHandlerInputSource)this).UpdateHandlers() || 
            AxisIsActive(aCRightTrigger) || AxisIsActive(aCLeftTrigger) ||
            AxisIsActive(aCHoriz) || AxisIsActive(aCVert) || AxisIsActive(aCDPadX) || AxisIsActive(aCDPadY)) {
            Container.MarkActive(this);
        }
    }
}
}