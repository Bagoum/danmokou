using System;
using UnityEngine;

namespace Danmokou.Core.DInput {

public enum ControllerType {
    XBox,
    PS4
}
/// <summary>
/// Lightweight representation of the physical object providing input information.
/// </summary>
public abstract record InputObject {
    public record KeyboardMouse : InputObject;

    public record Controller(ControllerType Type, int ControllerIndex) : InputObject {
        //Based on number defined in ProjectSettings
        public const int MAX_ALLOWED_CONTROLLERS = 3;
        public static readonly int JoystickDelta = (int)KeyCode.Joystick2Button0 - (int)KeyCode.Joystick1Button0;

        /// <summary>
        /// Converts JoystickNButtonY to JoystickButtonY, where N = <see cref="ControllerIndex"/>
        /// </summary>
        public KeyCode ReduceToBase(KeyCode kc) =>
            kc is >= KeyCode.Joystick1Button0 and <= KeyCode.Joystick8Button19 ?
                kc - (ControllerIndex + 1) * JoystickDelta :
                kc;
        
        /// <summary>
        /// Converts JoystickButtonY to JoystickNButtonY, where N = <see cref="ControllerIndex"/>
        /// </summary>
        public KeyCode GetJoystickSpecificKey(KeyCode kc) => 
            kc is >= KeyCode.JoystickButton0 and <= KeyCode.JoystickButton19 ?
                kc + (ControllerIndex + 1) * JoystickDelta :
                kc;

        public string GetVirtualAxis(ControllerAxis axis) =>
            ControllerAxisHelpers.VirtualAxisName(ControllerIndex, axis);
    }
    public static KeyboardMouse KBM { get; } = new KeyboardMouse();

    public static Controller? FromJoystickName(string joystickName, int index) {
        if (string.IsNullOrWhiteSpace(joystickName))
            return null;
        if (joystickName.ToLower().Contains("xbox"))
            return new Controller(ControllerType.XBox, index);
        return new Controller(ControllerType.PS4, index);
    }
}
}