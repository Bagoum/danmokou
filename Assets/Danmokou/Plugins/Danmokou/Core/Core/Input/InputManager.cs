using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.DMath;
using Newtonsoft.Json;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static FileUtils;
using ProtoBuf;
using static Danmokou.Core.DInput.KeyCodeHelpers;


namespace Danmokou.Core.DInput {
public static class InputManager {
    public static readonly ShiftKeyBinding Shift = new();
    public static readonly AltKeyBinding Alt = new();
    public static readonly CtrlKeyBinding Ctrl = new();
    public static readonly CmdKeyBinding Cmd = new();
    
    
    /// <summary>
    /// Input keys that can be inspected and used for rebinding at runtime.
    /// Includes non-reserved keycode and mousekey inputs, but not joystick inputs.
    /// </summary>
    public static readonly IInspectableInputBinding[] RebindableKeys;
    public static readonly IInspectableInputBinding[] RebindableControllerKeys;
    public static readonly Dictionary<IInspectableInputBinding, int> KeyOrdering;
    

    /// <summary>
    /// Returns a canonicalized list of the KBM keys that are currently being held.
    /// </summary>
    public static IInspectableInputBinding[]? CurrentlyHeldRebindableKeys {
        get {
            HashSet<IInspectableInputBinding>? ret = null;
            void Add(IInspectableInputBinding x) => (ret ??= new()).Add(x);
            foreach (var x in RebindableKeys)
                if (x.Active)
                    Add(x);
            return ret?.OrderBy(k => KeyOrdering[k]).ToArray();
        }
    }
    /// <summary>
    /// Returns a canonicalized list of the  controller keys that are currently being held.
    /// </summary>
    public static IInspectableInputBinding[]? CurrentlyHeldRebindableControllerKeys {
        get {
            HashSet<IInspectableInputBinding>? ret = null;
            void Add(IInspectableInputBinding x) => (ret ??= new()).Add(x);
            foreach (var x in RebindableControllerKeys)
                if (x.Active)
                    Add(x);
            return ret?.OrderBy(k => KeyOrdering[k]).ToArray();
        }
    }

    /// <summary>
    /// Parse the keys that were pressed Down on this frame and treat them as text input.
    /// Handles shift but not capslock.
    /// </summary>
    public static char? TextInput {
        //Note: while there exist keycodes like KeyCode.Asterisk, pressing shift+8 will not produce it.
        // Presumably, you need a special keyboard that has the asterisk key.
        get {
            bool capitalize = Shift.Active;
            foreach (var kc in TextInputKeys)
                if (Input.GetKeyDown(kc)) {
                    var k = capitalize ? kc.Capitalize() : kc;
                    if (k.RenderAsText() is { } c) {
                        if (capitalize && k.IsAlphabetic())
                            return (char)(c + ('A' - 'a'));
                        return c;
                    }
                }
            return null;
        }
    }

    static InputManager() {
        RebindableKeys = new IInspectableInputBinding[] {
            Ctrl, Cmd, Alt, Shift, 
            new KBMKeyInputBinding(KeyCode.UpArrow), new KBMKeyInputBinding(KeyCode.RightArrow),
            new KBMKeyInputBinding(KeyCode.DownArrow), new KBMKeyInputBinding(KeyCode.LeftArrow),
            //Left/right click are used for confirm/context menu on UI; they must be reported by the target element.
            //new MouseKeyInputBinding(0), new MouseKeyInputBinding(1), 
            new MouseKeyInputBinding(2)
        }.Concat(TextInputKeys.Select(ti => new KBMKeyInputBinding(ti))).ToArray();
        var controllerKeys = new List<IInspectableInputBinding>();
        foreach (var axis in Enum.GetValues(typeof(ControllerAxis)).Cast<ControllerAxis>()) {
            controllerKeys.Add(new AnyControllerInputBinding.Axis(axis, true));
            controllerKeys.Add(new AnyControllerInputBinding.Axis(axis, false));
        }
        for (var ii = KeyCode.JoystickButton0; ii < KeyCode.Joystick1Button0; ++ii)
            controllerKeys.Add(new AnyControllerInputBinding.Key(ii));
        RebindableControllerKeys = controllerKeys.ToArray();
        KeyOrdering = new();
        foreach (var (i, x) in RebindableKeys.Enumerate())
            KeyOrdering[x] = i;
        foreach (var (i, x) in RebindableControllerKeys.Enumerate())
            KeyOrdering[x] = i;
        PlayerInput.AddSource(InCodeInput, AggregateInputSource.REPLAY_PRIORITY + 1);
        unsafe {
            Logs.Log($"Replay frame size (should be 6): {sizeof(FrameInput)}.");
        }
    }
    
    /* New controls must be added in the following places:
     * Below, as `Type IsCtrl => PlayerInput.Ctrl ?? (default)`
     * In AggregateInputSource, as `Type? Ctrl => Aggregate(x => x.Ctrl)`
     * In IInputSource, as `Type? Ctrl => null`
     * In NullInputSource, as `Type? Ctrl => (default)`
     * In IDescriptiveInputSource, as `IInputHandler ctrl { get; }` and `Type? IInputSource.Ctrl => ctrl.Logic`
     * In IKeyedInputSource, in the Handlers.AddRange call
     * In ControllerInputSource and KBMInputSource, as `public IInputHandler fly { get; }` with logic
     * As RebindableInputBindings in InputConfig
     * If the control is stored in replays, then in the InputExtractor defined in the relevant GameDef.
     */
    
    public static bool DialogueConfirm => PlayerInput.DialogueConfirm ?? false;
    public static bool DialogueSkipAll => PlayerInput.DialogueSkipAll ?? false;

    public static short HorizontalSpeed => PlayerInput.HorizontalSpeed ?? 0;
    public static float HorizontalSpeed01 => HorizontalSpeed / (float) IInputSource.maxSpeed;

    public static short VerticalSpeed => PlayerInput.VerticalSpeed ?? 0;
    public static float VerticalSpeed01 => VerticalSpeed / (float) IInputSource.maxSpeed;
    public static bool IsFocus => PlayerInput.Focus ?? false;
    public static bool IsBomb => PlayerInput.Bomb ?? false;
    public static bool IsMeter => PlayerInput.Meter ?? false;
    public static bool IsSwap => PlayerInput.Swap ?? false;
    public static bool IsFly => PlayerInput.Fly ?? false;
    public static bool IsSlowFall => PlayerInput.SlowFall ?? false;
    public static bool IsFiring => PlayerInput.Firing ?? false;

    public static bool Pause => PlayerInput.Pause ?? false;
    public static bool VNBacklogPause => PlayerInput.VNBacklogPause ?? false;
    public static bool UIConfirm => PlayerInput.UIConfirm ?? false;
    public static bool UIBack => PlayerInput.UIBack ?? false;
    public static bool UIContextMenu => PlayerInput.UIContextMenu ?? false;
    public static bool UILeft => PlayerInput.UILeft ?? false;
    public static bool UIRight => PlayerInput.UIRight ?? false;
    public static bool UIUp => PlayerInput.UIUp ?? false;
    public static bool UIDown => PlayerInput.UIDown ?? false;

    public static IInputHandler GetKeyTrigger(KeyCode key) => PlayerInput.GetKeyTrigger(key);
    
    //Called by GameManagement
    public static void OncePerUnityFrameToggleControls() {
        PlayerInput.OncePerUnityFrameToggleControls();
    }

    public static InCodeInputSource InCodeInput { get; } = new();
    public static AggregateInputSource PlayerInput { get; } = new(new MainInputSource());
    public static IPrimaryInputSource MainSource => PlayerInput.MainSource.Current;

}
}
