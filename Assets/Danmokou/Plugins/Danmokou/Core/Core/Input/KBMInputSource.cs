using System.Collections.Generic;
using BagoumLib;
using UnityEngine;
using static Danmokou.Core.DInput.InputSettings;
using static Danmokou.Core.LocalizedStrings.Controls;

namespace Danmokou.Core.DInput {

public class KBMInputSource : IKeyedInputSource, IPrimaryInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    public MainInputSource Container { get; set; } = null!;
    public KBMInputSource() {
        ((IKeyedInputSource)this).AddUpdaters();
    }

    public IInputHandler arrowLeft { get; } = InputHandler.Trigger(i.Left, left);
    public IInputHandler arrowRight { get; } = InputHandler.Trigger(i.Right, right);
    public IInputHandler arrowUp { get; } = InputHandler.Trigger(i.Up, up);
    public IInputHandler arrowDown { get; } = InputHandler.Trigger(i.Down, down);
    
    public IInputHandler focusHold { get; } = InputHandler.Hold(i.FocusHold, focus);
    public IInputHandler fireHold { get; } = InputHandler.Hold(i.ShootHold, fire);
    public IInputHandler bomb { get; } = InputHandler.Trigger(i.Special, special);
    public IInputHandler meter { get; } = InputHandler.Hold(i.Special, special);
    public IInputHandler swap { get; } = InputHandler.Trigger(i.Swap, LocalizedStrings.Controls.swap);
    public IInputHandler pause { get; } =InputHandler.Trigger(i.Pause, LocalizedStrings.Controls.pause);
    public IInputHandler vnBacklogPause { get; } = InputHandler.Trigger(i.Backlog, backlog);
    public IInputHandler uiConfirm { get; } = InputHandler.Trigger(i.Confirm, confirm);
    
    
    public IInputHandler uiBack { get; } = InputHandler.Trigger(i.Back, back);
    public IInputHandler dialogueSkipAll { get; } = InputHandler.Trigger(i.SkipDialogue, skip);
    
    
    public short? HorizontalSpeed =>
        i.Right.Active ? IInputSource.maxSpeed : i.Left.Active ? IInputSource.minSpeed : (short)0;
    public short? VerticalSpeed =>
        i.Up.Active ? IInputSource.maxSpeed : i.Down.Active ? IInputSource.minSpeed : (short)0;
    
    void IInputSource.OncePerUnityFrameToggleControls() {
        if (((IInputHandlerInputSource)this).OncePerUnityFrameUpdateHandlers()) {
            Container.MarkActive(this);
        }
    }
}

}