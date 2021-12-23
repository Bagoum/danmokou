using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.DMath;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static FileUtils;
using ProtoBuf;
using UnityEngine.SocialPlatforms;


namespace Danmokou.Core {
public enum InputTriggerMethod {
    ONCE,
    ONCE_TOGGLE,
    PERSISTENT
}

/// <summary>
/// An input method that, on any frame, is either active or inactive.
/// </summary>
public interface IInputChecker {
    public bool Active { get; }
}
public class InputChecker {
    protected readonly Func<bool> checker;
    public readonly LString keyDescr;
    private readonly bool isController;

    public bool Active => (!isController || InputManager.AllowControllerInput) && checker();

    public InputChecker(Func<bool> check, LString desc, bool isController=false) {
        checker = check;
        keyDescr = desc;
        this.isController = isController;
    }
    //Use this combiner when there are multiple keys that do the same thing
    public InputChecker Or(InputChecker other) => 
        new InputChecker(() => Active || other.Active, 
            LString.Format(new LString("{0} or {1}", (Locales.JP, "{0}や{1}")), keyDescr, other.keyDescr));
    
    public InputChecker OrSilent(IInputChecker other) => 
        new InputChecker(() => Active || other.Active, keyDescr);
}

public class MockInputChecker : IInputChecker {
    private int activeCt = 0;
    public bool Active => activeCt > 0;

    /// <summary>
    /// Sets the input as active for the next frame.
    /// </summary>
    /// <returns></returns>
    public void SetForFrame() {
        InputManager.QueueOnInputUpdate(() => {
            ++activeCt;
            InputManager.QueueOnInputUpdate(() => {
                --activeCt;
            });
        });
    }
}

public interface IInputHandler {
    bool Active { get; }
    LString Desc { get; }
    void Update();
}
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
    public InputChecker checker;
    public LString Desc => checker.keyDescr;

    private InputHandler(InputTriggerMethod method, InputChecker check) {
        refractory = false;
        trigger = method;
        checker = check;
    }
    
    public static IInputHandler Toggle(InputChecker check) => new InputHandler(InputTriggerMethod.ONCE_TOGGLE, check);
    public static IInputHandler Hold(InputChecker check) => new InputHandler(InputTriggerMethod.PERSISTENT, check);
    public static IInputHandler Trigger(InputChecker check) => new InputHandler(InputTriggerMethod.ONCE, check);

    public void Update() {
        var keyDown = checker.Active;
        if (!refractory && keyDown) {
            refractory = trigger == InputTriggerMethod.ONCE || trigger == InputTriggerMethod.ONCE_TOGGLE;
            if (trigger == InputTriggerMethod.ONCE_TOGGLE) _active = toggledValue = !toggledValue;
            else _active = true;
        } else {
            if (refractory && !keyDown) refractory = false;
            _active = (trigger == InputTriggerMethod.ONCE_TOGGLE) ? toggledValue : false;
        }
    }
}
//Use this combiner when multiple keys combine to form one command (eg. ctrl+shift+R)
public class AndInputHandler : IInputHandler {
    private readonly IInputHandler[] parts;
    private readonly LString desc;
    public bool Active {
        get {
            for (int ii = 0; ii < parts.Length; ++ii) {
                if (!parts[ii].Active)
                    return false;
            }
            return true;
        }
    }
    public LString Desc => desc;


    public AndInputHandler(params IInputHandler[] parts) {
        this.parts = parts;
        this.desc = new LString(string.Join("+", parts.Select(p => p.Desc)));
    }
    
    public void Update() {
        for (int ii = 0; ii < parts.Length; ++ii)
            parts[ii].Update();
    }
    
}
public static class InputManager {
    private static readonly Queue<Action> onInputUpdate = new();
    public static void QueueOnInputUpdate(Action cb) => onInputUpdate.Enqueue(cb);
    public static bool AllowControllerInput { get; set; }
    public static readonly IReadOnlyList<KC> Alphanumeric = new[] {
        KC.A, KC.B, KC.C, KC.D, KC.E, KC.F, KC.G, KC.H, KC.I, KC.J, KC.K, KC.L, KC.M, KC.N,
        KC.O, KC.P, KC.Q, KC.R, KC.S, KC.T, KC.U, KC.V, KC.W, KC.X, KC.Y, KC.Z,
        KC.Alpha0, KC.Alpha1, KC.Alpha2, KC.Alpha3, KC.Alpha4,
        KC.Alpha5, KC.Alpha6, KC.Alpha7, KC.Alpha8, KC.Alpha9
    };

    private static readonly List<IInputHandler> CustomInputHandlers = new();
    private static readonly Dictionary<KC, IInputHandler> KeyTriggers = new();

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
    private static InputChecker Key(KC key, bool controller = false) => Key(() => key, controller);

    private static InputChecker Mouse(int key) =>
        new InputChecker(() => Input.GetMouseButton(key), new LString(key.ToString()));
    private static InputChecker Key(Func<KC> key, bool controller) =>
        new InputChecker(() => Input.GetKey(key()), new LString(key().ToString()), controller);

    private static InputChecker AxisL0(string axis, bool controller = false) =>
        new InputChecker(() => Input.GetAxisRaw(axis) < -0.1f, new LString(axis), controller);

    private static InputChecker AxisG0(string axis, bool controller = false) =>
        new InputChecker(() => Input.GetAxisRaw(axis) > 0.1f, new LString(axis), controller);

    private static readonly InputChecker ArrowRight = Key(KeyCode.RightArrow);
    private static readonly InputChecker ArrowLeft = Key(KeyCode.LeftArrow);
    private static readonly InputChecker ArrowUp = Key(KeyCode.UpArrow);
    private static readonly InputChecker ArrowDown = Key(KeyCode.DownArrow);

    public static readonly MockInputChecker ExternalUIConfirm = new();
    public static readonly MockInputChecker ExternalUISkipAllDialogue = new();
    
    public static readonly IInputHandler
        FocusHold = InputHandler.Hold(Key(i.FocusHold).Or(AxisG0(aCRightTrigger, true)));
    public static readonly IInputHandler ShootHold = InputHandler.Hold(Key(i.ShootHold).Or(AxisG0(aCLeftTrigger, true)));
    public static readonly IInputHandler Bomb = InputHandler.Trigger(Key(i.Special).Or(Key(cX, true)));
    public static readonly IInputHandler Meter = InputHandler.Hold(Key(i.Special).Or(Key(cX, true)));
    public static readonly IInputHandler Swap = InputHandler.Trigger(Key(i.Swap));

    public static readonly IInputHandler UILeft = InputHandler.Trigger(ArrowLeft.Or(AxisL0(aCDPadX, true)));
    public static readonly IInputHandler UIRight = InputHandler.Trigger(ArrowRight.Or(AxisG0(aCDPadX, true)));
    public static readonly IInputHandler UIUp = InputHandler.Trigger(ArrowUp.Or(AxisG0(aCDPadY, true)));
    public static readonly IInputHandler UIDown = InputHandler.Trigger(ArrowDown.Or(AxisL0(aCDPadY, true)));

    //mouse button 0, 1, 2 = left, right, middle click
    //don't listen to mouse left click for confirm-- left clicks need to be reported by the targeted elemnt
    public static readonly IInputHandler UIConfirm = InputHandler.Trigger(
        Key(KC.Z).Or(Key(KC.Return)).Or(Key(KC.Space)).Or(Key(cA, true)).OrSilent(ExternalUIConfirm));
    public static readonly IInputHandler UIBack = InputHandler.Trigger(Key(KC.X).Or(Key(cB, true)).Or(Mouse(1)));
    private static readonly IInputHandler UISkipAllDialogue = InputHandler.Trigger(Key(KC.LeftControl)
        .OrSilent(ExternalUISkipAllDialogue));

    public static readonly IInputHandler Pause = InputHandler.Trigger(
#if WEBGL
//Esc is reserved in WebGL
        Key(KC.BackQuote).Or(Key(cStart, true))
#else
        Key(KC.BackQuote).Or(Key(KC.Escape)).Or(Key(cStart, true))
#endif
        );
    public static readonly IInputHandler VNBacklogPause = InputHandler.Trigger(Key(KeyCode.L));

    public static readonly IInputHandler ReplayDebugSave = new AndInputHandler(
        InputHandler.Hold(Key(KC.LeftControl)),
        InputHandler.Hold(Key(KC.LeftShift)),
        InputHandler.Trigger(Key(KC.R))
    );

    public static IInputHandler GetKeyTrigger(KC key) {
        if (!KeyTriggers.TryGetValue(key, out var v)) {
            CustomInputHandlers.Add(v = KeyTriggers[key] = InputHandler.Trigger(Key(key)));
            v.Update();
        }
        return v;
    }

    static InputManager() {
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
        //This was formerly dialogueToEnd, but the separate key for dialogueToEnd was removed in v8.1.0.
        public bool UNUSED => data1.NthBool(5);
        public bool dialogueSkipAll => data1.NthBool(6);

        public FrameInput(short horiz, short vert, bool fire, bool focus, bool bomb, bool meter,
            bool dialogueConfirm, bool unused, bool dialogueSkip) {
            horizontal = horiz;
            vertical = vert;
            data1 = BitCompression.FromBools(fire, focus, bomb, meter, dialogueConfirm, unused, dialogueSkip);
        }
    }

    public static FrameInput RecordFrame => new FrameInput(HorizontalSpeed, VerticalSpeed,
        IsFiring, IsFocus, IsBomb, IsMeter, DialogueConfirm, false, DialogueSkipAll);

    public static bool DialogueConfirm => replay?.dialogueConfirm ?? UIConfirm.Active;
    public static bool DialogueSkipAll => replay?.dialogueSkipAll ?? UISkipAllDialogue.Active;

    private static FrameInput? replay = null;
    public static void ReplayFrame(FrameInput? fi) => replay = fi;

    private static readonly IInputHandler[] Updaters = {
        FocusHold, ShootHold, Bomb,
        UIDown, UIUp, UILeft, UIRight, UIConfirm, UIBack, UISkipAllDialogue, Pause, VNBacklogPause,
        Meter, Swap,
        ReplayDebugSave
    };

    private const short shortRef = short.MaxValue;
    private static float GetAxisRawC(string key) => AllowControllerInput ? Input.GetAxisRaw(key) : 0;
    private static float FRight => ArrowRight.Active ? 1 : 0;
    private static float _horizSpeed01 =>
        (ArrowRight.Active ? 1 : 0) + (ArrowLeft.Active ? -1 : 0) +
        GetAxisRawC(aCHoriz) + GetAxisRawC(aCDPadX);
    private static short _horizSpeedShort => M.ClampS(-shortRef, shortRef, (short)(_horizSpeed01 * shortRef));
    private static short HorizontalSpeed => replay?.horizontal ?? _horizSpeedShort;
    public static float HorizontalSpeed01 => HorizontalSpeed / (float) shortRef;

    private static float _vertSpeed01 =>
        (ArrowUp.Active ? 1 : 0) + (ArrowDown.Active ? -1 : 0) +
        GetAxisRawC(aCVert) + GetAxisRawC(aCDPadY);
    private static short _vertSpeedShort => M.ClampS(-shortRef, shortRef, (short)(_vertSpeed01 * shortRef));
    private static short VerticalSpeed => replay?.vertical ?? _vertSpeedShort;
    public static float VerticalSpeed01 => VerticalSpeed / (float) shortRef;
    public static bool IsFocus => replay?.focus ?? FocusHold.Active;
    public static bool IsBomb => replay?.bomb ?? Bomb.Active;
    public static bool IsMeter => replay?.meter ?? Meter.Active;
    public static bool IsSwap => Swap.Active;
    public static bool IsFiring => replay?.fire ?? ShootHold.Active;
    
    

    //Called by GameManagement
    public static void OncePerFrameToggleControls() {
        //Don't handle any cbs pushed during the handling process-- this is required for proper MockInput support
        var ninv = onInputUpdate.Count;
        while (ninv-- > 0)
            onInputUpdate.Dequeue()();
        
        for (int ii = 0; ii < Updaters.Length; ++ii)
            Updaters[ii].Update();
        for (int ii = 0; ii < CustomInputHandlers.Count; ++ii)
            CustomInputHandlers[ii].Update();
    }

}
}
