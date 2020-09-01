using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static SaveUtils;

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
    public bool Active { get; private set; }
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
        var isActive = checker.checker();
        if (!refractory && isActive) {
            refractory = trigger == InputTriggerMethod.ONCE || trigger == InputTriggerMethod.ONCE_TOGGLE;
            if (trigger == InputTriggerMethod.ONCE_TOGGLE) Active = toggledValue = !toggledValue;
            else Active = true;
        } else {
            if (refractory && !isActive) refractory = false;
            Active = (trigger == InputTriggerMethod.ONCE_TOGGLE) ? toggledValue : false;
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
    
    
    //public static readonly InputHandler FocusToggle = InputHandler.Toggle(Key(KC.Space).Or(Key(cRightShoulder)));
    public static readonly InputHandler FocusHold = InputHandler.Hold(Key(i.FocusHold).Or(AxisG0(aCRightTrigger)));
    public static readonly InputHandler AimLeft = InputHandler.Trigger(Key(i.AimLeft).Or(AxisL0(aCRightX)));
    public static readonly InputHandler AimRight = InputHandler.Trigger(Key(i.AimRight).Or(AxisG0(aCRightX)));
    public static readonly InputHandler AimUp = InputHandler.Trigger(Key(i.AimUp).Or(AxisG0(aCRightY)));
    public static readonly InputHandler AimDown = InputHandler.Trigger(Key(i.AimDown).Or(AxisL0(aCRightY)));
    public static readonly InputHandler ShootToggle = InputHandler.Toggle(Key(i.ShootToggle).Or(Key(cLeftShoulder)));
    public static readonly InputHandler ShootHold = InputHandler.Hold(Key(i.ShootHold).Or(AxisG0(aCLeftTrigger)));
    
    public static readonly InputHandler UILeft = InputHandler.Trigger(AxisL0(aHoriz).Or(AxisL0(aCDPadX)));
    public static readonly InputHandler UIRight = InputHandler.Trigger(AxisG0(aHoriz).Or(AxisG0(aCDPadX)));
    public static readonly InputHandler UIUp = InputHandler.Trigger(AxisG0(aVert).Or(AxisG0(aCDPadY)));
    public static readonly InputHandler UIDown = InputHandler.Trigger(AxisL0(aVert).Or(AxisL0(aCDPadY)));
    
    public static readonly InputHandler UIConfirm = InputHandler.Trigger(Key(KC.Z).Or(Key(cA)));
    public static readonly InputHandler UIBack = InputHandler.Trigger(Key(KC.X).Or(Key(cB)));
    private static readonly InputHandler UISkipDialogue = InputHandler.Trigger(Key(KC.LeftControl));
    
    public static readonly InputHandler Pause = InputHandler.Trigger(Key(KC.Escape).Or(Key(cStart)));
    
    [Serializable]
    public struct FrameInput {
        //16 bytes (14 unpadded)
        // float(4)x2 = 8
        // enum(1)    = 1
        // bool(1)x5  = 5
        public float horizontal;
        public float vertical;
        public ShootDirection shootDir;
        public bool fire;
        public bool focus;
        public bool dialogueConfirm;
        public bool dialogueToEnd;
        public bool dialogueSkip;
    }
    
    public static FrameInput RecordFrame => new FrameInput() {
        horizontal = HorizontalSpeed,
        vertical = VerticalSpeed,
        shootDir = FiringDir,
        fire = IsFiring,
        focus = IsFocus,
        dialogueConfirm = DialogueConfirm,
        dialogueToEnd = DialogueToEnd,
        dialogueSkip = DialogueSkip,
    };

    public static bool DialogueConfirm => replay?.dialogueConfirm ?? UIConfirm.Active;
    public static bool DialogueToEnd => replay?.dialogueToEnd ?? UIBack.Active;
    public static bool DialogueSkip => replay?.dialogueSkip ?? UISkipDialogue.Active;

    private static FrameInput? replay = null;
    public static void ReplayFrame(FrameInput? fi) => replay = fi;

    private static readonly InputHandler[] Updaters = {
        //FocusToggle, 
        FocusHold, AimLeft, AimRight, AimUp, AimDown, ShootToggle, ShootHold,
        UIDown, UIUp, UILeft, UIRight, UIConfirm, UIBack, UISkipDialogue, Pause
    };


    private static KeyCode editorReloadHook = KeyCode.R;
    public static float HorizontalSpeed => replay?.horizontal ?? 
                                           (Input.GetAxisRaw(aHoriz) + Input.GetAxisRaw(aCDPadX));

    public static float VerticalSpeed => replay?.vertical ??
                                         (Input.GetAxisRaw(aVert) + Input.GetAxisRaw(aCDPadY));


    //Called by GameManagement
    public static void OncePerFrameToggleControls() {
        foreach (var u in Updaters) u.Update();
        //Debug.Log(UIDown.Active);
/*
        foreach (var v in Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>()) {
            if (Input.GetKey(v)) Debug.Log($"Keypress {v}");
        }*/
        //foreach (var axis in new[] {"ControllerRTrigger", "ControllerLTrigger"}) Debug.Log($"Axis {axis}: {Input.GetAxis(axis)}");

    }

    public static bool IsFocus => replay?.focus ?? FocusHold.Active;
    public static ShootDirection FiringDir { get {
        if (replay.HasValue) return replay.Value.shootDir;
    #if VER_SIMP
        if (AimUp.Active) return ShootDirection.UP;
        if (AimRight.Active) return ShootDirection.RIGHT;
        if (AimLeft.Active) return ShootDirection.LEFT;
        if (AimDown.Active) return ShootDirection.DOWN;
    #endif
        return ShootDirection.INHERIT;
    }}
    public static float? FiringAngle => FiringDir.ToAngle();

#if VER_SIMP
    public static bool IsFiring => replay?.fire ?? (ShootHold.Active || ShootToggle.Active);
#else
    public static bool IsFiring => ShootHold.Active;
#endif

    public static bool EditorReloadActivated() {
        return Input.GetKeyDown(editorReloadHook);
    }

}
