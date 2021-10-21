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

public class InputChecker {
    private readonly Func<bool> checker;
    public readonly LString keyDescr;
    private readonly bool isController;

    public bool Active => (!isController || InputManager.AllowControllerInput) && checker();

    public InputChecker(Func<bool> f, LString k, bool isController=false) {
        checker = f;
        keyDescr = k;
        this.isController = isController;
    }
    //Use this combiner when there are multiple keys that do the same thing
    public InputChecker Or(InputChecker other) => 
        new InputChecker(() => Active || other.Active, 
            LString.Format(new LString("{0} or {1}", (Locales.JP, "{0}や{1}")), keyDescr, other.keyDescr));
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
    public static bool AllowControllerInput { get; set; }
    public static readonly IReadOnlyList<KC> Alphanumeric = new[] {
        KC.A, KC.B, KC.C, KC.D, KC.E, KC.F, KC.G, KC.H, KC.I, KC.L, KC.M, KC.N,
        KC.O, KC.P, KC.Q, KC.R, KC.T, KC.U, KC.V, KC.W, KC.X, KC.Y, KC.Z,
        KC.Alpha0, KC.Alpha1, KC.Alpha2, KC.Alpha3, KC.Alpha4,
        KC.Alpha5, KC.Alpha6, KC.Alpha7, KC.Alpha8, KC.Alpha9
    };

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

    public static readonly IInputHandler UIConfirm = InputHandler.Trigger(
        Key(KC.Z).Or(Key(KC.Return)).Or(Key(KC.Space)).Or(Key(cA, true)));
    public static readonly IInputHandler UIBack = InputHandler.Trigger(Key(KC.X).Or(Key(cB, true)));
    private static readonly IInputHandler UISkipAllDialogue = InputHandler.Trigger(Key(KC.LeftControl));

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
        public bool dialogueToEnd => data1.NthBool(5);
        public bool dialogueSkip => data1.NthBool(6);

        public FrameInput(short horiz, short vert, bool fire, bool focus, bool bomb, bool meter,
            bool dialogueConfirm, bool dialogueToEnd, bool dialogueSkip) {
            horizontal = horiz;
            vertical = vert;
            data1 = BitCompression.FromBools(fire, focus, bomb, meter, dialogueConfirm, dialogueToEnd, dialogueSkip);
        }
    }

    public static FrameInput RecordFrame => new FrameInput(HorizontalSpeed, VerticalSpeed,
        IsFiring, IsFocus, IsBomb, IsMeter, DialogueConfirm, DialogueToEnd, DialogueSkipAll);

    public static bool DialogueConfirm => replay?.dialogueConfirm ?? UIConfirm.Active;
    public static bool DialogueToEnd => replay?.dialogueToEnd ?? UIBack.Active;
    public static bool DialogueSkipAll => replay?.dialogueSkip ?? UISkipAllDialogue.Active;

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


    //Called by GameManagement
    public static void OncePerFrameToggleControls() {
        foreach (var u in Updaters) u.Update();
    }

    public static bool IsFocus => replay?.focus ?? FocusHold.Active;
    public static bool IsBomb => replay?.bomb ?? Bomb.Active;
    public static bool IsMeter => replay?.meter ?? Meter.Active;
    public static bool IsSwap => Swap.Active;
    public static bool IsFiring => replay?.fire ?? ShootHold.Active;

}
}
