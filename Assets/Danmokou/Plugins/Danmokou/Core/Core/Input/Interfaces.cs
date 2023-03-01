using System.Collections.Generic;
using BagoumLib;

namespace Danmokou.Core.DInput {

/// <summary>
/// An input source that makes use of the <see cref="IInputHandler"/> abstraction for its provided keys.
/// </summary>
public interface IInputHandlerInputSource {
    List<IInputHandler> Handlers { get; }
    public bool AnyKeyPressedThisFrame { get; protected set; }

    /// <summary>
    /// Update all linked <see cref="IInputHandler"/>s, and returns true if any of them were set to true.
    /// </summary>
    /// <returns></returns>
    bool OncePerUnityFrameUpdateHandlers() {
        bool anyActive = false;
        for (int ii = 0; ii < Handlers.Count; ++ii)
            anyActive |= Handlers[ii].OncePerUnityFrameUpdate();
        return AnyKeyPressedThisFrame = anyActive;
    }
}

/// <summary>
/// An input source that exposes self-describing <see cref="IInputHandler"/>s for its keys.
/// </summary>
public interface IDescriptiveInputSource : IInputSource {
    IInputHandler arrowLeft { get; }
    IInputHandler arrowRight { get; }
    IInputHandler arrowUp { get; }
    IInputHandler arrowDown { get; }
    
    IInputHandler focusHold { get; }
    IInputHandler fireHold { get; }
    IInputHandler bomb { get; }
    IInputHandler meter { get; }
    IInputHandler swap { get; }
    
    IInputHandler pause { get; }
    IInputHandler vnBacklogPause { get; }
    IInputHandler uiConfirm { get; }
    IInputHandler uiBack { get; }
    IInputHandler? dialogueSkipAll { get; }

}

/// <summary>
/// An <see cref="IDescriptiveInputSource"/> whose input values are directly derived from
///  the corresponding IInputHandlers.
/// </summary>
public interface IKeyedInputSource : IDescriptiveInputSource, IInputHandlerInputSource {
    void AddUpdaters() {
        Handlers.AddRange(new[] {
                arrowLeft, arrowRight, arrowUp, arrowDown,
                focusHold, fireHold, bomb, meter, swap,
                pause, vnBacklogPause, uiConfirm, uiBack, dialogueSkipAll
            }.FilterNone()
        );
    }
    bool? IInputSource.Firing => fireHold.Active;
    bool? IInputSource.Focus => focusHold.Active;
    bool? IInputSource.Bomb => bomb.Active;
    bool? IInputSource.Meter => meter.Active;
    bool? IInputSource.Swap => swap.Active;
    bool? IInputSource.Pause => pause.Active;
    bool? IInputSource.VNBacklogPause => vnBacklogPause.Active;
    bool? IInputSource.UIConfirm => uiConfirm.Active;
    bool? IInputSource.UIBack => uiBack.Active;
    
    bool? IInputSource.UILeft => arrowLeft.Active;
    bool? IInputSource.UIRight => arrowRight.Active;
    bool? IInputSource.UIUp => arrowUp.Active;
    bool? IInputSource.UIDown => arrowDown.Active;
    bool? IInputSource.DialogueConfirm => UIConfirm;
    bool? IInputSource.DialogueSkipAll => dialogueSkipAll?.Active;
}



}