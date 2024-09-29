
namespace Danmokou.UI.XML {
/// <summary>
/// View/model for supporting an absolute-positioned visual cursor targeted at this element.
/// <br/>This is automatically added if the node HTML contains an element with .cursor-target.
/// </summary>
public class VisualCursorTargetView : UIView<VisualCursorTargetView.Model>, IUIView {
    public record Model : IConstUIViewModel;
    
    public VisualCursorTargetView() : base(new()) { }

    public void OnEnter(UINode node, ICursorState cs, bool animate) {
        node.Controller.VisualCursor.SetTarget(this);
    }

    public void OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) {
        if (popupType is null or PopupUIGroup.Type.Popup)
            node.Controller.VisualCursor.UnsetTarget(this);
    }
}
}