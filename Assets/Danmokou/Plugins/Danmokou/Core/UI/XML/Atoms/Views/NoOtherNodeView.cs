namespace Danmokou.UI.XML {
/// <summary>
/// View/model for a node that should be displayed only when
///  no other node in the group is visible.
/// </summary>
public class NoOtherNodeView: UIView<NoOtherNodeView.Model> {
    public class Model: IConstUIViewModel {
        bool IUIViewModel.ShouldBeVisible(UINode node) {
            foreach (var n in node.Group.Nodes)
                if (n != node && n.IsNodeVisible)
                    return false;
            return true;
        }
    }
    public NoOtherNodeView() : base(new()) { }
}
}