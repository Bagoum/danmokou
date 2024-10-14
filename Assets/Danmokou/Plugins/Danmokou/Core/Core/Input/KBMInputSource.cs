using System.Collections.Generic;
using BagoumLib;
using UnityEngine;
using static Danmokou.Core.DInput.InputSettings;
using static Danmokou.Core.LocalizedStrings.Controls;

namespace Danmokou.Core.DInput {

public class KBMInputSource : IKeyedInputSource, IPrimaryInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    public bool AnyKeyPressedThisFrame { get; private set; }
    public int Priority => 1; //less priority because keyboard input may be shadowing controller input
                              //(notably in case of arrows shadowing dpad)
    bool IInputHandlerInputSource.AnyKeyPressedThisFrame {
        get => AnyKeyPressedThisFrame;
        set => AnyKeyPressedThisFrame = value;
    }
    public MainInputSource Container { get; set; } = null!;

    public KBMInputSource() {
        ((IKeyedInputSource)this).AddUpdaters();
    }

    public IInputHandler arrowLeft { get; } = InputHandler.TriggerRefire(i.Left, left);
    public IInputHandler arrowRight { get; } = InputHandler.TriggerRefire(i.Right, right);
    public IInputHandler arrowUp { get; } = InputHandler.TriggerRefire(i.Up, up);
    public IInputHandler arrowDown { get; } = InputHandler.TriggerRefire(i.Down, down);
    
    public IInputHandler focusHold { get; } = InputHandler.Hold(i.FocusHold, focus);
    public IInputHandler fireHold { get; } = InputHandler.Hold(i.ShootHold, fire);
    public IInputHandler bomb { get; } = InputHandler.Trigger(i.Special, special);
    public IInputHandler meter { get; } = InputHandler.Hold(i.Special, special);
    public IInputHandler swap { get; } = InputHandler.Trigger(i.Swap, LocalizedStrings.Controls.swap);
    public IInputHandler fly { get; } = InputHandler.Hold(i.Fly, LocalizedStrings.Controls.fly);
    public IInputHandler slowFall { get; } = InputHandler.Hold(i.SlowFall, LocalizedStrings.Controls.slowfall);
    public IInputHandler pause { get; } =InputHandler.Trigger(i.Pause, LocalizedStrings.Controls.pause);
    public IInputHandler vnBacklogPause { get; } = InputHandler.Trigger(i.Backlog, backlog);
    public IInputHandler uiConfirm { get; } = InputHandler.Trigger(i.Confirm, confirm);
    public IInputHandler uiBack { get; } = InputHandler.Trigger(i.Back, back);
    public IInputHandler uiContextMenu { get; } = InputHandler.Trigger(i.ContextMenu, back);
    public IInputHandler dialogueSkipAll { get; } = InputHandler.Trigger(i.SkipDialogue, skip);
    public IInputHandler leftClick { get; } = InputHandler.Trigger(new KBMKeyInputBinding(KeyCode.Mouse0), confirm);
    
    
    public short? HorizontalSpeed =>
        i.Right.Active ? IInputSource.maxSpeed : i.Left.Active ? IInputSource.minSpeed : (short)0;
    public short? VerticalSpeed =>
        i.Up.Active ? IInputSource.maxSpeed : i.Down.Active ? IInputSource.minSpeed : (short)0;
    
    bool IInputSource.OncePerUnityFrameToggleControls() {
        if (((IInputHandlerInputSource)this).OncePerUnityFrameUpdateHandlers()) {
            Container.MarkActive(this);
            return true;
        } else
            return false;
    }
}

}