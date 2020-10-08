using System;
using System.Collections.Generic;
using DMath;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static SaveUtils;
using ProtoBuf;

public enum InputTriggerMethod {
    ONCE,
    ONCE_TOGGLE,
    PERSISTENT
}

public readonly struct InputChecker {
    public readonly Func<bool> checker;
    public readonly string keyDescr;

    public InputChecker(Func<bool> f, string k) {
        checker = f;
        keyDescr = k;
    }
    
    public InputChecker Or(InputChecker other) => 
        new InputChecker(checker.Or(other.checker), $"{keyDescr} or {other.keyDescr}");
}

public class InputHandler {
    private bool refractory;
    private readonly InputTriggerMethod trigger;
    private bool toggledValue;
    public bool Active => _active &&
                          //Prevents events like Bomb from being triggered twice in two RU frames per one unity frame
                          (trigger == InputTriggerMethod.ONCE ?
                            ETime.FirstUpdateForScreen :
                            true);
    private bool _active;
    public InputChecker checker;
    public string Desc => checker.keyDescr;

    private InputHandler(InputTriggerMethod method, InputChecker check) {
        refractory = false;
        trigger = method;
        checker = check;
    }
    
    public static InputHandler Toggle(InputChecker check) => new InputHandler(InputTriggerMethod.ONCE_TOGGLE, check);
    public static InputHandler Hold(InputChecker check) => new InputHandler(InputTriggerMethod.PERSISTENT, check);
    public static InputHandler Trigger(InputChecker check) => new InputHandler(InputTriggerMethod.ONCE, check);

    public void Update() {
        var keyDown = checker.checker();
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
public static class InputManager {
    public static readonly IReadOnlyList<KC> Alphanumeric = new[] {
        KC.A, KC.B, KC.C, KC.D, KC.E, KC.F, KC.G, KC.H, KC.I, KC.L, KC.M, KC.N,
        KC.O, KC.P, KC.Q, KC.R, KC.T, KC.U, KC.V, KC.W, KC.X, KC.Y, KC.Z,
        KC.Alpha0, KC.Alpha1, KC.Alpha2, KC.Alpha3, KC.Alpha4, 
        KC.Alpha5, KC.Alpha6, KC.Alpha7, KC.Alpha8, KC.Alpha9
    };
    
    private const string aHoriz = "Horizontal";
    private const string aVert = "Vertical";
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
    private static InputChecker Key(KC key) => Key(() => key);
    private static InputChecker Key(Func<KC> key) => new InputChecker(() => Input.GetKey(key()), key().ToString());
    private static InputChecker AxisL0(string axis) => new InputChecker(() => Input.GetAxisRaw(axis) < -0.1f, axis);
    private static InputChecker AxisG0(string axis) => new InputChecker(() => Input.GetAxisRaw(axis) > 0.1f, axis);
    
    
    public static readonly InputHandler FocusHold = InputHandler.Hold(Key(i.FocusHold).Or(AxisG0(aCRightTrigger)));
    public static readonly InputHandler AimLeft = InputHandler.Trigger(Key(i.AimLeft).Or(AxisL0(aCRightX)));
    public static readonly InputHandler AimRight = InputHandler.Trigger(Key(i.AimRight).Or(AxisG0(aCRightX)));
    public static readonly InputHandler AimUp = InputHandler.Trigger(Key(i.AimUp).Or(AxisG0(aCRightY)));
    public static readonly InputHandler AimDown = InputHandler.Trigger(Key(i.AimDown).Or(AxisL0(aCRightY)));
    public static readonly InputHandler ShootHold = InputHandler.Hold(Key(i.ShootHold).Or(AxisG0(aCLeftTrigger)));
    public static readonly InputHandler Bomb = InputHandler.Trigger(Key(i.Bomb));
    public static readonly InputHandler Meter = InputHandler.Hold(Key(i.Bomb));
    
    public static readonly InputHandler UILeft = InputHandler.Trigger(AxisL0(aHoriz).Or(AxisL0(aCDPadX)));
    public static readonly InputHandler UIRight = InputHandler.Trigger(AxisG0(aHoriz).Or(AxisG0(aCDPadX)));
    public static readonly InputHandler UIUp = InputHandler.Trigger(AxisG0(aVert).Or(AxisG0(aCDPadY)));
    public static readonly InputHandler UIDown = InputHandler.Trigger(AxisL0(aVert).Or(AxisL0(aCDPadY)));
    
    public static readonly InputHandler UIConfirm = InputHandler.Trigger(Key(KC.Z).Or(Key(cA)));
    public static readonly InputHandler UIBack = InputHandler.Trigger(Key(KC.X).Or(Key(cB)));
    private static readonly InputHandler UISkipDialogue = InputHandler.Trigger(Key(KC.LeftControl));
    
    public static readonly InputHandler Pause = InputHandler.Trigger(Key(KC.Escape).Or(Key(cStart)));

    static InputManager() {
        unsafe {
            Log.Unity($"Replay frame size (should be 6): {sizeof(FrameInput)}.");
        }
    }
    [Serializable]
    [ProtoContract]
    public struct FrameInput {
        // 6-8 bytes (5 unpadded)
        // short(2)x2 = 4
        // byte(1)x1 = 1
        [ProtoMember(1)]
        public short horizontal;
        [ProtoMember(2)]
        public short vertical;
        [ProtoMember(3)]
        public byte data1;
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
        
#if FT_DIRSHOOT
        public ShootDirection shootDir;
#endif
    }
    
    public static FrameInput RecordFrame => new FrameInput(HorizontalSpeed, VerticalSpeed,
            IsFiring, IsFocus, IsBomb, IsMeter, DialogueConfirm, DialogueToEnd, DialogueSkip) {
#if FT_DIRSHOOT
            shootDir = FiringDir
#endif
    };

    public static bool DialogueConfirm => replay?.dialogueConfirm ?? UIConfirm.Active;
    public static bool DialogueToEnd => replay?.dialogueToEnd ?? UIBack.Active;
    public static bool DialogueSkip => replay?.dialogueSkip ?? UISkipDialogue.Active;

    private static FrameInput? replay = null;
    public static void ReplayFrame(FrameInput? fi) => replay = fi;

    private static readonly InputHandler[] Updaters = {
        FocusHold, AimLeft, AimRight, AimUp, AimDown, ShootHold, Bomb,
        UIDown, UIUp, UILeft, UIRight, UIConfirm, UIBack, UISkipDialogue, Pause,
        Meter,
        
    };

    private const float shortRef = short.MaxValue;
    private static float _horizSpeed01 => (Input.GetAxisRaw(aHoriz) + Input.GetAxisRaw(aCDPadX));
    private static short _horizSpeedShort => (short) M.Clamp(-shortRef, shortRef, _horizSpeed01 * shortRef);
    private static short HorizontalSpeed => replay?.horizontal ?? _horizSpeedShort;
    public static float HorizontalSpeed01 => HorizontalSpeed / (float)shortRef;
    
    private static float _vertSpeed01 => Input.GetAxisRaw(aVert) + Input.GetAxisRaw(aCDPadY);
    private static short _vertSpeedShort => (short) M.Clamp(-shortRef, shortRef, _vertSpeed01 * shortRef);
    private static short VerticalSpeed => replay?.vertical ?? _vertSpeedShort;
    public static float VerticalSpeed01 => VerticalSpeed / (float)shortRef;


    //Called by GameManagement
    public static void OncePerFrameToggleControls() {
        foreach (var u in Updaters) u.Update();
    }

    public static bool IsFocus => replay?.focus ?? FocusHold.Active;
    public static bool IsBomb => replay?.bomb ?? Bomb.Active;
    public static bool IsMeter => replay?.meter ?? Meter.Active;
    public static ShootDirection FiringDir { get {
    #if FT_DIRSHOOT
        if (replay.HasValue) return replay.Value.shootDir;
        if (AimUp.Active) return ShootDirection.UP;
        if (AimRight.Active) return ShootDirection.RIGHT;
        if (AimLeft.Active) return ShootDirection.LEFT;
        if (AimDown.Active) return ShootDirection.DOWN;
    #endif
        return ShootDirection.INHERIT;
    }}
    public static float? FiringAngle => FiringDir.ToAngle();

    public static bool IsFiring => replay?.fire ?? ShootHold.Active;


}
