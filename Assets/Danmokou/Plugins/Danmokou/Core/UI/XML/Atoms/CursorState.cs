namespace Danmokou.UI.XML {
/// <summary>
/// The current state of the cursor moving through a menu. In most cases this is <see cref="NullCursorState"/>,
///  which has no special behavior, but can be configured for menu-specific behavior.
/// </summary>
public interface ICursorState {
    /// <summary>
    /// Perform navigation on a given node for a given command.
    /// <br/>By default, this simply calls node.Navigate.
    /// </summary>
    UIResult Navigate(UINode node, UICommand cmd);
}

public class NullCursorState : ICursorState {
    public UIResult Navigate(UINode node, UICommand cmd) => node.Navigate(cmd, this);
}

}