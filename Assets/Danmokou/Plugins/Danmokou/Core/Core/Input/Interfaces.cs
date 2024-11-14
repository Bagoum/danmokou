using System.Collections.Generic;
using BagoumLib;
using UnityEngine.XR;

namespace Danmokou.Core.DInput {

/// <summary>
/// An input source that makes use of the <see cref="IInputHandler"/> abstraction for its provided keys.
/// </summary>
public interface IInputHandlerInputSource : IInputSource {
    List<IInputHandler> Handlers { get; }
    public bool AnyKeyPressedThisFrame { get; protected set; }

    /// <summary>
    /// Update all linked <see cref="IInputHandler"/>s, and returns true if any of them had activity.
    /// </summary>
    /// <returns></returns>
    bool OncePerUnityFrameUpdateHandlers() {
        bool anyActive = false;
        for (int ii = 0; ii < Handlers.Count; ++ii)
            anyActive |= Handlers[ii].OncePerUnityFrameUpdate();
        return AnyKeyPressedThisFrame = anyActive;
    }

    void IInputSource.Interrupt() => Interrupt(this);

    /// <inheritdoc cref="IInputHandler.Interrupt"/>
    public static void Interrupt(IInputHandlerInputSource me) {
        foreach (var h in me.Handlers)
            h.Interrupt();
    }
}

/// <summary>
/// An input source that exposes self-describing <see cref="IInputHandler"/>s for its keys.
/// </summary>
public interface IDescriptiveInputSource : IInputHandlerInputSource {
    IInputHandler arrowLeft { get; }
    bool? IInputSource.UILeft => arrowLeft.Active;
    IInputHandler arrowRight { get; }
    bool? IInputSource.UIRight => arrowRight.Active;
    IInputHandler arrowUp { get; }
    bool? IInputSource.UIUp => arrowUp.Active;
    IInputHandler arrowDown { get; }
    bool? IInputSource.UIDown => arrowDown.Active;
    
    IInputHandler focusHold { get; }
    bool? IInputSource.Focus => focusHold.Active;
    IInputHandler fireHold { get; }
    bool? IInputSource.Firing => fireHold.Active;
    IInputHandler bomb { get; }
    bool? IInputSource.Bomb => bomb.Active;
    IInputHandler meter { get; }
    bool? IInputSource.Meter => meter.Active;
    IInputHandler swap { get; }
    bool? IInputSource.Swap => swap.Active;
    IInputHandler fly { get; }
    bool? IInputSource.Fly => fly.Active;
    IInputHandler slowFall { get; }
    bool? IInputSource.SlowFall => slowFall.Active;
    
    IInputHandler pause { get; }
    bool? IInputSource.Pause => pause.Active;
    IInputHandler vnBacklogPause { get; }
    bool? IInputSource.VNBacklogPause => vnBacklogPause.Active;
    IInputHandler uiConfirm { get; }
    bool? IInputSource.UIConfirm => uiConfirm.Active;
    bool? IInputSource.DialogueConfirm => UIConfirm;
    IInputHandler uiBack { get; }
    bool? IInputSource.UIBack => uiBack.Active;
    IInputHandler uiContextMenu { get; }
    bool? IInputSource.UIContextMenu => uiContextMenu.Active;
    IInputHandler? dialogueSkipAll { get; }
    bool? IInputSource.DialogueSkipAll => dialogueSkipAll?.Active;
    IInputHandler? leftClick { get; }
    bool? IInputSource.LeftClick => leftClick?.Active;
    
    void AddUpdaters() {
        Handlers.AddRange(new[] {
                arrowLeft, arrowRight, arrowUp, arrowDown,
                focusHold, fireHold, bomb, meter, swap, fly, slowFall,
                pause, vnBacklogPause, uiConfirm, uiBack, uiContextMenu, dialogueSkipAll,
                leftClick
            }.FilterNone()
        );
    }
}



}