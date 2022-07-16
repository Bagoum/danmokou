using System.Collections.Generic;

namespace Danmokou.Core.DInput {
public enum ControllerAxis {
    AxisX,
    AxisY,
    Axis3,
    Axis4,
    Axis5,
    Axis6,
    Axis7,
    Axis8,
    Axis9,
    Axis10
}

public static class ControllerAxisHelpers {
    public static readonly Dictionary<ControllerAxis, string> AxisKeyMapping = new() {
        { ControllerAxis.AxisX, "X" },
        { ControllerAxis.AxisY, "Y" },
        { ControllerAxis.Axis3, "3" },
        { ControllerAxis.Axis4, "4" },
        { ControllerAxis.Axis5, "5" },
        { ControllerAxis.Axis6, "6" },
        { ControllerAxis.Axis7, "7" },
        { ControllerAxis.Axis8, "8" },
        { ControllerAxis.Axis9, "9" },
        { ControllerAxis.Axis10, "10" },
    };

    private static readonly Dictionary<(int, ControllerAxis), string> vaxisMemo = new();
    /// <summary>
    /// Get the virtual axis name (defined in Project Settings > Input Manager)
    /// for the controller index (0-indexed) and controller axis.
    /// <br/>This function is memoized.
    /// </summary>
    public static string VirtualAxisName(int joystickIndex, ControllerAxis axis) {
        if (!vaxisMemo.TryGetValue((joystickIndex, axis), out var v))
            v = vaxisMemo[(joystickIndex, axis)] = $"C{joystickIndex + 1}A{AxisKeyMapping[axis]}";
        return v;
    }
}

}