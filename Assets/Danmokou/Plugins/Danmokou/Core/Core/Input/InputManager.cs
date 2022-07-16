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

/// <summary>
/// Method that governs when an <see cref="InputHandler"/> is active as depending on its underlying sources.
/// </summary>
public enum InputTriggerMethod {
    /// <summary>
    /// The handler is active on the first Regular Update frame
    /// for the Unity frame when the source was first made active.
    /// </summary>
    ONCE,
    /// <summary>
    /// The handler is continuously active starting from the first Regular Update frame
    /// for the Unity frame when the source was first made active,
    /// and is deactivated when the source is made active again.
    /// </summary>
    ONCE_TOGGLE,
    /// <summary>
    /// The handler is active while the source is active.
    /// </summary>
    PERSISTENT
}
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
            //new MouseKeyInputBinding(0), don't allow modifying left-click, it will fuck with how it is used by default
            new MouseKeyInputBinding(1), new MouseKeyInputBinding(2)
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

    [Serializable]
    [ProtoContract]
    public struct FrameInput {
        // 6-8 bytes (5 unpadded)
        // short(2)x2 = 4
        // byte(1)x1 = 1
        [ProtoMember(1)] public short horizontal;
        [ProtoMember(2)] public short vertical;
        [ProtoMember(3)] public byte data1;
        public bool fire => data1.NthBool(0);
        public bool focus => data1.NthBool(1);
        public bool bomb => data1.NthBool(2);
        public bool meter => data1.NthBool(3);
        public bool dialogueConfirm => data1.NthBool(4);
        public bool swap => data1.NthBool(5);
        public bool dialogueSkipAll => data1.NthBool(6);

        public FrameInput(short horiz, short vert, bool fire, bool focus, bool bomb, bool meter,
            bool dialogueConfirm, bool swap, bool dialogueSkip) {
            horizontal = horiz;
            vertical = vert;
            data1 = BitCompression.FromBools(fire, focus, bomb, meter, dialogueConfirm, swap, dialogueSkip);
        }
    }

    public static FrameInput RecordFrame => new FrameInput(HorizontalSpeed, VerticalSpeed,
        IsFiring, IsFocus, IsBomb, IsMeter, DialogueConfirm, IsSwap, DialogueSkipAll);

    public static bool DialogueConfirm => PlayerInput.DialogueConfirm ?? false;
    public static bool DialogueSkipAll => PlayerInput.DialogueSkipAll ?? false;

    private static short HorizontalSpeed => PlayerInput.HorizontalSpeed ?? 0;
    public static float HorizontalSpeed01 => HorizontalSpeed / (float) IInputSource.maxSpeed;

    private static short VerticalSpeed => PlayerInput.VerticalSpeed ?? 0;
    public static float VerticalSpeed01 => VerticalSpeed / (float) IInputSource.maxSpeed;
    public static bool IsFocus => PlayerInput.Focus ?? false;
    public static bool IsBomb => PlayerInput.Bomb ?? false;
    public static bool IsMeter => PlayerInput.Meter ?? false;
    public static bool IsSwap => PlayerInput.Swap ?? false;
    public static bool IsFiring => PlayerInput.Firing ?? false;

    public static bool Pause => PlayerInput.Pause ?? false;
    public static bool VNBacklogPause => PlayerInput.VNBacklogPause ?? false;
    public static bool UIConfirm => PlayerInput.UIConfirm ?? false;
    public static bool UIBack => PlayerInput.UIBack ?? false;
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
    public static IDescriptiveInputSource MainSource => PlayerInput.MainSource.Current;

}
}
