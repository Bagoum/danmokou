using System;
using System.Linq;
using BagoumLib.Culture;
using Newtonsoft.Json;
using UnityEngine;

namespace Danmokou.Core.DInput {

/// <summary>
/// A representation of user input that, at any time, is either active or inactive.
/// <example><see cref="KBMKeyInputBinding"/>(KeyCode.D) is active while the D key is held down.</example>
/// </summary>
public interface IInputBinding {
    /// <summary>
    /// A readable string describing the activation input (eg. "left click").
    /// May change frame-to-frame.
    /// </summary>
    public string Description { get; }
    public bool Active { get; }

    //Use this combiner when there are multiple keys that do the same thing
    IInputBinding Or(IInputBinding other) => 
        new GenericInputBinding(() => Active || other.Active, 
            () => Locales.TextLocale switch {
                Locales.JP => $"{Description}や{other.Description}",
                _ => $"{Description} or {other.Description}"
            });
    
    IInputBinding OrSilent(IInputBinding other) => 
        new GenericInputBinding(() => Active || other.Active, () => Description);
}

/// <summary>
/// An input binding that has value-based equality and can be used to inspect and rebind inputs at runtime.
/// Must be serializable.
/// </summary>
public interface IInspectableInputBinding : IInputBinding { }
public class GenericInputBinding : IInputBinding {
    private readonly Func<bool> checker;
    private readonly Func<string> description;
    [JsonIgnore] public bool Active => checker();
    [JsonIgnore] public string Description => description();
    public GenericInputBinding(Func<bool> check, Func<string> desc) {
        checker = check;
        description = desc;
    }
}

/// <summary>
/// For inputs that require more than one key, eg. Ctrl+Shift+R
/// </summary>
[Serializable]
public record SimultaneousInputBinding(params IInspectableInputBinding[] Parts) : IInspectableInputBinding {
    [JsonIgnore] public string Description => string.Join('+', Parts.Select(p => p.Description));
    [JsonIgnore] public bool Active {
        get {
            foreach (var p in Parts)
                if (!p.Active)
                    return false;
            return true;
        }
    }

    public static IInspectableInputBinding FromMany(IInspectableInputBinding[] parts) =>
        parts.Length == 1 ? parts[0] : new SimultaneousInputBinding(parts);
}

[Serializable]
public record KBMKeyInputBinding(KeyCode Key) : IInspectableInputBinding {
    [JsonIgnore]
    public string Description => Key.RenderInformative();

    [JsonIgnore] public bool Active => Input.GetKey(Key);
}

/// <summary>
/// An input binding that fires when the corresponding input is made on *any* controller.
/// These can be rebound and saved, but are not generally used in-game since we generally
/// want to get controller-specific input.
/// </summary>
public abstract record AnyControllerInputBinding : IInspectableInputBinding {
    public abstract string Description { get; }
    public abstract bool Active { get; }

    public abstract ControllerInputBinding Realize(InputObject.Controller c);

    public record Key(KeyCode BaseKey) : AnyControllerInputBinding {
        [JsonIgnore] public override string Description =>
            DescriptionFor(BaseKey, InputManager.PlayerInput.MainSource.GetFirstControllerType());
        [JsonIgnore] public override bool Active =>
            Input.GetKey(BaseKey);

        public override ControllerInputBinding Realize(InputObject.Controller c) =>
            new ControllerInputBinding.Key(BaseKey, c);

        public static string DescriptionFor(KeyCode kc, ControllerType? ct) {
            string ByController(string xbox, string ps4) =>
                ct switch {
                    ControllerType.PS4 => ps4,
                    _ => xbox
                };
            return kc switch {
                //Using font https://shinmera.github.io/promptfont/
                KeyCode.JoystickButton0 => ByController("⇓", "⇣"),
                KeyCode.JoystickButton1 => ByController("⇒", "⇢"),
                KeyCode.JoystickButton3 => ByController("⇑", "⇡"),
                KeyCode.JoystickButton2 => ByController("⇐", "⇠"),
                KeyCode.JoystickButton4 => ByController("↘", "↰"),
                KeyCode.JoystickButton5 => ByController("↙", "↱"),
                KeyCode.JoystickButton6 => ByController("⇺", "⇦"),
                KeyCode.JoystickButton7 => ByController("⇻", "⇨"),
                _ => kc.RenderInformative()
            };
        }
    }

    public record Axis(ControllerAxis CAxis, bool UseGTComparison) : AnyControllerInputBinding {
        public const float Cutoff = 0.1f;
        [JsonIgnore] public override string Description =>
            DescriptionFor(CAxis, UseGTComparison, InputManager.PlayerInput.MainSource.GetFirstControllerType());
        [JsonIgnore]
        public override bool Active {
            get {
                for (int ii = 0; ii < InputObject.Controller.MAX_ALLOWED_CONTROLLERS; ++ii) {
                    var level = Input.GetAxisRaw(ControllerAxisHelpers.VirtualAxisName(ii, CAxis));
                    if (UseGTComparison ?
                        level > Cutoff :
                        level < -Cutoff)
                        return true;
                }
                return false;
            }
        }

        public override ControllerInputBinding Realize(InputObject.Controller c) =>
            new ControllerInputBinding.Axis(CAxis, c, UseGTComparison, UseGTComparison ? Cutoff : -Cutoff);
        
        public Axis Flip() => new(CAxis, !UseGTComparison);
        
        public static string DescriptionFor(ControllerAxis axis, bool usegt, ControllerType? ct) {
            string ByDir(string left, string right) =>  usegt ? right : left;

            string ByDirController(string xboxl, string xboxr, string ps4l, string ps4r) => ct switch {
                ControllerType.PS4 => ByDir(ps4l, ps4r),
                _ => ByDir(xboxl, xboxr)
            };
            string ByController(string xbox, string ps4) => ct switch {
                ControllerType.PS4 => ps4,
                _ => xbox
            };
            return axis switch {
                //Using font https://shinmera.github.io/promptfont/
                ControllerAxis.AxisX => ByDir("↼", "⇀"),
                ControllerAxis.AxisY => ByDir("⇂", "↾"),
                ControllerAxis.Axis3 => "Axis 3 (unconfigured)",
                ControllerAxis.Axis4 => ByDir("↽", "⇁"),
                ControllerAxis.Axis5 => ByDir("⇃", "↿"),
                ControllerAxis.Axis6 => ByDir("↞", "↠"),
                ControllerAxis.Axis7 => ByDir("↡", "↟"),
                ControllerAxis.Axis8 => "Axis 8 (unconfigured)",
                ControllerAxis.Axis9 => ByController("↖", "↲"),
                ControllerAxis.Axis10 => ByController("↗", "↳"),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}

/// <summary>
/// An input binding for a specific attached controller.
/// These are constructed internally at runtime.
/// </summary>
public abstract record ControllerInputBinding(InputObject.Controller MyController) : IInputBinding {
    private ControllerType Type => MyController.Type;
    public abstract string Description { get; }
    public abstract bool Active { get; }

    public record Key(KeyCode BaseKey, InputObject.Controller MyController) : ControllerInputBinding(MyController) {
        private string ByController(string xbox, string ps4) =>
            Type switch {
                ControllerType.PS4 => ps4,
                _ => xbox
            };

        [JsonIgnore] public override string Description => 
            AnyControllerInputBinding.Key.DescriptionFor(BaseKey, MyController.Type);
        [JsonIgnore] public override bool Active => 
            MyController is { } c && Input.GetKey(c.GetJoystickSpecificKey(BaseKey));
    }

    public record Axis(ControllerAxis CAxis, InputObject.Controller MyController, bool UseGTComparison, float Cutoff) : 
            ControllerInputBinding(MyController) {
        private string ByDir(string left, string right) => UseGTComparison ? right : left;

        private string ByDirController(string xboxl, string xboxr, string ps4l, string ps4r) => Type switch {
            ControllerType.PS4 => ByDir(ps4l, ps4r),
            _ => ByDir(xboxl, xboxr)
        };
        private string ByController(string xbox, string ps4) => Type switch {
            ControllerType.PS4 => ps4,
            _ => xbox
        };

        [JsonIgnore] public override string Description => CAxis switch {
            //Using font https://shinmera.github.io/promptfont/
            ControllerAxis.AxisX => ByDir("↼", "⇀"),
            ControllerAxis.AxisY => ByDir("⇂", "↾"),
            ControllerAxis.Axis3 => "Axis 3 (unconfigured)",
            ControllerAxis.Axis4 => ByDir("↽", "⇁"),
            ControllerAxis.Axis5 => ByDir("⇃", "↿"),
            ControllerAxis.Axis6 => ByDir("↞", "↠"),
            ControllerAxis.Axis7 => ByDir("↡", "↟"),
            ControllerAxis.Axis8 => "Axis 8 (unconfigured)",
            ControllerAxis.Axis9 => ByController("↖", "↲"),
            ControllerAxis.Axis10 => ByController("↗", "↳"),
            _ => throw new ArgumentOutOfRangeException()
        };
        [JsonIgnore] public float AxisValue => 
            MyController is { } c ? Input.GetAxisRaw(c.GetVirtualAxis(CAxis)) : 0;
        [JsonIgnore] public override bool Active {
            get {
                var level = AxisValue;
                return UseGTComparison ?
                    level > Cutoff :
                    level < Cutoff;
            }
        }
        [JsonIgnore] public bool IsNonzero => Mathf.Abs(AxisValue) > 0.01f;

        public Axis Flip() => new(CAxis, MyController, !UseGTComparison, -Cutoff);
    }
}

public abstract record DualKeyBinding(KeyCode key1, KeyCode key2) : IInspectableInputBinding {
    public abstract string Description { get; }
    [JsonIgnore] public bool Active => Input.GetKey(key1) || Input.GetKey(key2);
}

[Serializable]
public record ShiftKeyBinding() : DualKeyBinding(KeyCode.LeftShift, KeyCode.RightShift) {
    [JsonIgnore] public override string Description => "Shift";
}
[Serializable]
public record AltKeyBinding() : DualKeyBinding(KeyCode.LeftAlt, KeyCode.RightAlt) {
    [JsonIgnore] public override string Description => "Alt";
}
[Serializable]
public record CtrlKeyBinding() : DualKeyBinding(KeyCode.LeftControl, KeyCode.RightControl) {
    [JsonIgnore] public override string Description => "Ctrl";
}
[Serializable]
public record CmdKeyBinding() : DualKeyBinding(KeyCode.LeftCommand, KeyCode.RightCommand) {
    [JsonIgnore] public override string Description => "Cmd";
}



[Serializable]
public record MouseKeyInputBinding(int Key) : IInspectableInputBinding {
    [JsonIgnore] public string Description => Key switch {
        //https://shinmera.github.io/promptfont/
        0 => "⟵",
        1 => "⟶",
        2 => "⟷",
        _ => "Unknown click"
    };
    [JsonIgnore] public bool Active => Input.GetMouseButton(Key);

}

public class MockInputBinding : IInputBinding {
    private readonly InCodeInputSource source;
    private int activeCt = 0;
    public string Description => "";
    public bool Active => activeCt > 0;

    public MockInputBinding(InCodeInputSource source) {
        this.source = source;
    }

    public void SetActive() => source.SetActive(this);

    public int _AddCounter(int delta) => activeCt += delta;
}

/// <summary>
/// An input method which may layer update-dependent limitations over some <see cref="IInputBinding"/>s.
/// <example><see cref="InputHandler"/>.Trigger(KeyInputBinding(KeyCode.D)) is active on the frame
/// that the D key is pressed, but not while it is held.</example>
/// </summary>
public interface IInputHandler {
    bool Active { get; }
    /// <summary>
    /// A readable string describing the activation input (eg. "left click").
    /// May change frame-to-frame.
    /// </summary>
    string Description { get; }
    /// <summary>
    /// Description of what the input does in-game (eg. 'Confirm', 'Move left')
    /// </summary>
    public LString Purpose { get; }
    /// <summary>
    /// Update the state of the input method.
    /// </summary>
    /// <returns>True iff the input method is now active.</returns>
    bool OncePerUnityFrameUpdate();
}

/// <summary>
/// See <see cref="IInputHandler"/>
/// </summary>
public class InputHandler : IInputHandler {
    private bool refractory;
    private readonly InputTriggerMethod trigger;
    private bool toggledValue;
    public bool Active => _active && EngineStateManager.State.InputAllowed() &&
                          //Prevents events like Bomb from being triggered twice in two RU frames per one unity frame
                          (trigger == InputTriggerMethod.ONCE ?
                            ETime.FirstUpdateForScreen :
                            true);
    private bool _active;
    public readonly IInputBinding binding;
    public string Description => binding.Description;
    public LString Purpose { get; }

    private InputHandler(InputTriggerMethod method, IInputBinding check, LString purpose) {
        refractory = false;
        trigger = method;
        binding = check;
        Purpose = purpose;
    }
    
    public static IInputHandler Toggle(IInputBinding check, LString? purpose = null) => 
        new InputHandler(InputTriggerMethod.ONCE_TOGGLE, check, 
            purpose ?? (check as IPurposefulInputBinding)?.Purpose ?? 
            "(This key handler has no defined purpose. This should not display.)");
    public static IInputHandler Hold(IInputBinding check, LString? purpose = null) => 
        new InputHandler(InputTriggerMethod.PERSISTENT, check, 
            purpose ?? (check as IPurposefulInputBinding)?.Purpose ?? 
            "(This key handler has no defined purpose. This should not display.)");
    public static IInputHandler Trigger(IInputBinding check, LString? purpose = null) => 
        new InputHandler(InputTriggerMethod.ONCE, check, 
            purpose ?? (check as IPurposefulInputBinding)?.Purpose ?? 
            "(This key handler has no defined purpose. This should not display.)");

    public bool OncePerUnityFrameUpdate() {
        var keyDown = binding.Active;
        if (!refractory && keyDown) {
            refractory = trigger is InputTriggerMethod.ONCE or InputTriggerMethod.ONCE_TOGGLE;
            if (trigger == InputTriggerMethod.ONCE_TOGGLE) _active = toggledValue = !toggledValue;
            else _active = true;
        } else {
            if (refractory && !keyDown) refractory = false;
            _active = (trigger == InputTriggerMethod.ONCE_TOGGLE) ? toggledValue : false;
        }
        return _active;
    }
}
}