namespace Danmokou.UI.XML {
public class XMLMainMenu_NLBug : UIController {
    public override void FirstFrame() {
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined){ Builder = (s, ve) => {
            s.Margin.SetLRMargin(480, 480);
            var c = ve.AddColumn();
            c.style.maxWidth = 20f.Percent();
            c.style.paddingTop = 120;
        }};
        _ = new UIColumn(MainScreen, null, new[] {
            new UINode("Replays"),
            new UINode("Replays").With("large1"),
            new UINode("Stage 1"),
            new UINode("Stage 2"),
            new UINode("Stage2"),
            new UINode("Stage 2a"),
            new UINode("Stage 3"),
            new UINode("Stage 4"),
            new UINode("Stage 5"),
            new UINode("Stage 6")
            
        });
        base.FirstFrame();
    }
}
}