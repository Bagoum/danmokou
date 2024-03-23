using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using BagoumLib.Culture;
using BagoumLib.Events;
using Newtonsoft.Json;
using UnityEngine;
using static Danmokou.Core.LocalizedStrings.Controls;

namespace Danmokou.Core.DInput {

public interface IPurposefulInputBinding : IInputBinding {
    /// <summary>
    /// Description of what the input does in-game (eg. 'Confirm', 'Move left')
    /// </summary>
    public LString Purpose { get; }
}

/// <summary>
/// A control that can be displayed and rebound on a menu screen,
///  backed by a set (usually two) of alternative inputs.
/// </summary>
[Serializable]
public class RebindableInputBinding : IInputBinding {
    public IInputBinding?[] Sources { get; init; }
    public LString Purpose { get; init; }

    [JsonIgnore] private List<(int index, IInputBinding? newBinding)> newBindings = new();
    public HashSet<int> ProtectedIndices = new();
    [JsonIgnore] public Event<Unit> BindingsChanged = new();

    public RebindableInputBinding(LString purpose, params IInputBinding?[] sources) {
        this.Purpose = purpose;
        this.Sources = sources;
    }
    [JsonIgnore]
    public string Description {
        get {
            var pieces = ListCache<string>.Get();
            foreach (var b in Sources)
                if (b != null)
                    pieces.Add(b.Description);
            var result = StringBuffer.JoinPooled(IInputBinding.OrConnector, pieces);
            ListCache<string>.Consign(pieces);
            return result;
        }
    }
    [JsonIgnore]
    public bool Active {
        get {
            foreach (var b in Sources)
                if (b is { Active: true })
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Disallow the binding at the provided index in <see cref="Sources"/> from being rebound.
    /// </summary>
    public RebindableInputBinding Protect(int index) {
        ProtectedIndices.Add(index);
        return this;
    }

    public void AddNewBinding(int index, IInputBinding? binding) {
        newBindings.Add((index, binding));
    }

    public bool RevokeNewBinding(int index) {
        bool foundAny = false;
        for (int ii = 0; ii < newBindings.Count; ++ii) {
            if (index == newBindings[ii].index) {
                newBindings.RemoveAt(ii);
                foundAny = true;
            }
        }
        return foundAny;
    }

    public void ChangeBindingAt(int index, IInputBinding? binding) {
        Sources[index] = binding;
        BindingsChanged.OnNext(default);
    }
    
    public void ApplyNewBindings() {
        foreach (var (index, newBinding) in newBindings) {
            Sources[index] = newBinding;
        }
        BindingsChanged.OnNext(default);
    }

    public void ClearNewBindings() {
        newBindings.Clear();
    }
    
}
public class InputConfig {
    //---
    //--- Keyboard inputs
    //---
    public RebindableInputBinding FocusHold { get; init; } = 
        new(focus, new ShiftKeyBinding(), null);
    public RebindableInputBinding ShootHold { get; init; } = 
        new(fire, new KBMKeyInputBinding(KeyCode.Z), null);
    public RebindableInputBinding Special { get; init; } = 
        new(special, new KBMKeyInputBinding(KeyCode.X), null);
    public RebindableInputBinding Swap { get; init; } = 
        new(swap, new KBMKeyInputBinding(KeyCode.Space), null);
    
    public RebindableInputBinding Fly { get; init; } = 
        new(fly, new KBMKeyInputBinding(KeyCode.Space), null);
    public RebindableInputBinding SlowFall { get; init; } = 
        new(slowfall, new ShiftKeyBinding(), null);

    public RebindableInputBinding Left { get; init; } =
        new(left, new KBMKeyInputBinding(KeyCode.LeftArrow), new KBMKeyInputBinding(KeyCode.A));
    public RebindableInputBinding Right { get; init; } =
        new(right, new KBMKeyInputBinding(KeyCode.RightArrow), new KBMKeyInputBinding(KeyCode.D));
    public RebindableInputBinding Up { get; init; } =
        new(up, new KBMKeyInputBinding(KeyCode.UpArrow), new KBMKeyInputBinding(KeyCode.W));
    public RebindableInputBinding Down { get; init; } =
        new(down, new KBMKeyInputBinding(KeyCode.DownArrow), new KBMKeyInputBinding(KeyCode.S));
    
    public RebindableInputBinding Confirm { get; init; } =
        new(confirm, new KBMKeyInputBinding(KeyCode.Z), new KBMKeyInputBinding(KeyCode.Return));
    
    //mouse button 0, 1, 2 = left, right, middle click
    //don't listen to mouse left click for confirm-- left clicks need to be reported by the targeted element
    //(left click cannot be rebound)
    public RebindableInputBinding Back { get; init; } =
        new(back, new KBMKeyInputBinding(KeyCode.X), new MouseKeyInputBinding(1)
#if UNITY_ANDROID
//System back button is mapped to ESC
            , new KeyInputBinding(KeyCode.Escape)
#endif
        );
    
    public RebindableInputBinding ContextMenu { get; init; } =
        new(contextmenu, new KBMKeyInputBinding(KeyCode.C), null);

    public RebindableInputBinding Pause { get; init; } =
        new RebindableInputBinding(pause, new KBMKeyInputBinding(KeyCode.BackQuote),
#if WEBGL || UNITY_ANDROID
//ESC is reserved in WebGL, and is mapped to the back button in Android
            null);
#else
            new KBMKeyInputBinding(KeyCode.Escape)).Protect(1);
#endif
    public RebindableInputBinding SkipDialogue { get; init; } =
        new(skip, new CtrlKeyBinding(), null);
    public RebindableInputBinding Backlog { get; init; } =
        new(backlog, new KBMKeyInputBinding(KeyCode.L), null);

    //---
    //--- Controller inputs
    //---
    private static readonly AnyControllerInputBinding.Axis LJoyX = new(ControllerAxis.AxisX, true);
    private static readonly AnyControllerInputBinding.Axis LJoyY = new(ControllerAxis.AxisY, true);
    private static readonly AnyControllerInputBinding.Axis DPadX = new(ControllerAxis.Axis6, true);
    private static readonly AnyControllerInputBinding.Axis DPadY = new(ControllerAxis.Axis7, true);
    
    public RebindableInputBinding CFocusHold { get; init; } = 
        //r2
        new(focus, new AnyControllerInputBinding.Axis(ControllerAxis.Axis10, true), null);
    public RebindableInputBinding CShootHold { get; init; } = 
        //l2
        new(fire, new AnyControllerInputBinding.Axis(ControllerAxis.Axis9, true), null);
    public RebindableInputBinding CSpecial { get; init; } = 
        //X
        new(special, new AnyControllerInputBinding.Key(KeyCode.JoystickButton2), null);
    public RebindableInputBinding CSwap { get; init; } = 
        //Y
        new(swap, new AnyControllerInputBinding.Key(KeyCode.JoystickButton3), null);
    
    public RebindableInputBinding CFly { get; init; } = 
        //Y
        new(fly, new AnyControllerInputBinding.Key(KeyCode.JoystickButton3), null);
    
    public RebindableInputBinding CSlowFall { get; init; } = 
        //r2
        new(slowfall, new AnyControllerInputBinding.Axis(ControllerAxis.Axis10, true), null);

    public RebindableInputBinding CLeft { get; init; } =
        new(left, LJoyX.Flip(), DPadX.Flip());
    public RebindableInputBinding CRight { get; init; } =
        new(right, LJoyX, DPadX);
    public RebindableInputBinding CUp { get; init; } =
        new(up, LJoyY, DPadY);
    public RebindableInputBinding CDown { get; init; } =
        new(down, LJoyY.Flip(), DPadY.Flip());
    
    public RebindableInputBinding CConfirm { get; init; } =
        //A
        new(confirm, new AnyControllerInputBinding.Key(KeyCode.JoystickButton0), null);
    
    public RebindableInputBinding CBack { get; init; } =
        //B
        new(back, new AnyControllerInputBinding.Key(KeyCode.JoystickButton1), null);
    public RebindableInputBinding CContextMenu { get; init; } =
        //Select
        new(contextmenu, new AnyControllerInputBinding.Key(KeyCode.JoystickButton6), null);

    public RebindableInputBinding CPause { get; init; } =
        //Start
        new RebindableInputBinding(pause, new AnyControllerInputBinding.Key(KeyCode.JoystickButton7), null);
    
    public RebindableInputBinding CSkipDialogue { get; init; } =
        new(skip, null, null);
    
    public RebindableInputBinding CBacklog { get; init; } =
        //Select
        new(backlog, new AnyControllerInputBinding.Key(KeyCode.JoystickButton6), null);
    
    [JsonIgnore]
    public RebindableInputBinding[] KBMBindings => new[] {
        FocusHold,
        ShootHold,
        Special,
        Swap, Fly, SlowFall,
        Left, Right, Up, Down,
        Confirm, Back, ContextMenu,
        Pause, SkipDialogue, Backlog
    };
    [JsonIgnore]
    public RebindableInputBinding[] ControllerBindings => new[] {
        CFocusHold,
        CShootHold,
        CSpecial,
        CSwap, CFly, CSlowFall,
        CLeft, CRight, CUp, CDown,
        CConfirm, CBack, CContextMenu,
        CPause, CSkipDialogue, CBacklog
    };
}

}