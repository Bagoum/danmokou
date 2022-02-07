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


namespace Danmokou.Core.DInput {
public enum InputTriggerMethod {
    ONCE,
    ONCE_TOGGLE,
    PERSISTENT
}

/// <summary>
/// A simple input method that, at any time, is either active or inactive.
/// </summary>
public interface IInputChecker {
    public LString Description { get; }
    public bool Active { get; }
}
public class InputChecker : IInputChecker {
    protected readonly Func<bool> checker;
    public LString Description { get; }

    public bool Active => checker();

    public InputChecker(Func<bool> check, LString desc) {
        checker = check;
        Description = desc;
    }
    //Use this combiner when there are multiple keys that do the same thing
    public InputChecker Or(InputChecker other) => 
        new InputChecker(() => Active || other.Active, 
            LString.Format(new LText("{0} or {1}", (Locales.JP, "{0}や{1}")), Description, other.Description));
    
    public InputChecker OrSilent(IInputChecker other) => 
        new InputChecker(() => Active || other.Active, Description);
}

public class MockInputChecker : IInputChecker {
    private readonly InCodeInputSource source;
    private int activeCt = 0;
    public LString Description { get; } = LString.Empty;
    public bool Active => activeCt > 0;

    public MockInputChecker(InCodeInputSource source) {
        this.source = source;
    }

    public void SetActive() => source.SetActive(this);

    public int _AddCounter(int delta) => activeCt += delta;
}

/// <summary>
/// An input method which may layer update-dependent limitations over some <see cref="IInputChecker"/>s.
/// </summary>
public interface IInputHandler {
    bool Active { get; }
    LString Description { get; }
    /// <summary>
    /// Update the state of the input method.
    /// </summary>
    /// <returns>True iff the input method is now active.</returns>
    bool Update();
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
    public readonly IInputChecker checker;
    public LString Description => checker.Description;

    private InputHandler(InputTriggerMethod method, IInputChecker check) {
        refractory = false;
        trigger = method;
        checker = check;
    }
    
    public static IInputHandler Toggle(IInputChecker check) => new InputHandler(InputTriggerMethod.ONCE_TOGGLE, check);
    public static IInputHandler Hold(IInputChecker check) => new InputHandler(InputTriggerMethod.PERSISTENT, check);
    public static IInputHandler Trigger(IInputChecker check) => new InputHandler(InputTriggerMethod.ONCE, check);

    public bool Update() {
        var keyDown = checker.Active;
        if (!refractory && keyDown) {
            refractory = trigger == InputTriggerMethod.ONCE || trigger == InputTriggerMethod.ONCE_TOGGLE;
            if (trigger == InputTriggerMethod.ONCE_TOGGLE) _active = toggledValue = !toggledValue;
            else _active = true;
        } else {
            if (refractory && !keyDown) refractory = false;
            _active = (trigger == InputTriggerMethod.ONCE_TOGGLE) ? toggledValue : false;
        }
        return _active;
    }
}
//Use this combiner when multiple keys combine to form one command (eg. ctrl+shift+R)
public class AndInputHandler : IInputHandler {
    private readonly IInputHandler[] parts;
    public bool Active {
        get {
            for (int ii = 0; ii < parts.Length; ++ii) {
                if (!parts[ii].Active)
                    return false;
            }
            return true;
        }
    }
    public LString Description { get; }


    public AndInputHandler(params IInputHandler[] parts) {
        this.parts = parts;
        this.Description = LString.FormatFn(p => string.Join("+", p), parts.Select(p => p.Description).ToArray());
    }
    
    public bool Update() {
        bool allValid = true;
        for (int ii = 0; ii < parts.Length; ++ii)
            allValid &= parts[ii].Update();
        return allValid;
    }
    
}
public static class InputManager {
    public static readonly IReadOnlyList<KC> Alphanumeric = new[] {
        KC.A, KC.B, KC.C, KC.D, KC.E, KC.F, KC.G, KC.H, KC.I, KC.J, KC.K, KC.L, KC.M, KC.N,
        KC.O, KC.P, KC.Q, KC.R, KC.S, KC.T, KC.U, KC.V, KC.W, KC.X, KC.Y, KC.Z,
        KC.Alpha0, KC.Alpha1, KC.Alpha2, KC.Alpha3, KC.Alpha4,
        KC.Alpha5, KC.Alpha6, KC.Alpha7, KC.Alpha8, KC.Alpha9
    };
    

    static InputManager() {
        unsafe {
            Logs.Log($"Replay frame size (should be 6): {sizeof(FrameInput)}.");
            PlayerInput.AddSource(InCodeInput, AggregateInputSource.REPLAY_PRIORITY + 1);
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
    public static AggregateInputSource PlayerInput { get; } = new(
        new MainInputSource(new KBMInputSource(), new ControllerInputSource()));
    public static IDescriptiveInputSource MainSource => PlayerInput.MainSource.Current;

}
}
