using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;

namespace Danmokou.UI.XML {
/// <summary>
/// The current state of the cursor moving through a menu. In most cases this is <see cref="NullCursorState"/>,
///  which has no special behavior, but custom cursors can be configured for menu-specific behavior.
/// </summary>
public interface ICursorState {
    /// <summary>
    /// Perform navigation when the pointer is used to target a new node.
    /// </summary>
    UIResult? PointerGoto(UINode current, UINode target) => new UIResult.GoToNode(target);

    /// <summary>
    /// Perform navigation based on arbitrary external events on a given current node.
    /// <br/>This overrides <see cref="Navigate"/>.
    /// </summary>
    UIResult? CustomEventHandling(UINode current) => current.CustomEventHandling();
    
    /// <summary>
    /// Perform navigation on a given node for a given command.
    /// </summary>
    UIResult Navigate(UINode current, UICommand cmd);
}

public class NullCursorState : ICursorState {
    public UIResult Navigate(UINode current, UICommand cmd) => current.Navigate(cmd, this);
}

public abstract class CustomCursorState : ICursorState, ITokenized {
    public List<IDisposable> Tokens { get; } = new();
    public UIController Controller { get; }

    public CustomCursorState(UIController controller) {
        Tokens.Add((Controller = controller).CursorState.AddConst(this));
    }
    public abstract UIResult Navigate(UINode current, UICommand cmd);

    public virtual void Destroy() {
        Tokens.DisposeAll();
    }

}

}