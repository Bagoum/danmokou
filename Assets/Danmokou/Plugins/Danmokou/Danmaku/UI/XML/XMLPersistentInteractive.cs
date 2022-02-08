namespace Danmokou.UI.XML {
/// <summary>
/// An XML menu that is visible and controllable at all times. It has a single freeform group,
///  and nodes representing on-screen targets can be dynamically added or removed.
/// <br/>When there are no added nodes, any inputs will result in a silent no-op.
/// </summary>
public class XMLPersistentInteractive : UIController {
    private UINode unselect = null!;

    public override void FirstFrame() {
        unselect = new EmptyNode(0, 0);
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => {
                ve.AddColumn();
                s.Margin.SetLRMargin(0, 0);
            }
        };

        var g = new UIFreeformGroup(MainScreen, unselect);
        base.FirstFrame();
        
        //testing
        //g.AddNodeDynamic(new UINode("hello world"));
    }
}
}