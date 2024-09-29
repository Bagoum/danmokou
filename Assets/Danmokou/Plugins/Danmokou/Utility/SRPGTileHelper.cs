using System;
using Danmokou.Behavior;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine.UIElements;

namespace Danmokou.Plugins.Danmokou.Utility {
public class SRPGTileHelper : CoroutineRegularUpdater, IFixedXMLReceiver {
    private FixedXMLHelper xml = null!;
    private LocalXMLSRPGExamples menu = null!;
    private (int r, int c) index;

    private void Awake() {
        xml = GetComponent<FixedXMLHelper>();
        xml.Receiver = this;
    }

    public UINode Initialize(LocalXMLSRPGExamples _menu, (int, int) _index) {
        this.menu = _menu;
        this.index = _index;
        return xml.MakeNode();
    }
    
    //TODO use adjacencies to navigate?
    UIResult? IFixedXMLReceiver.Navigate(UINode n, ICursorState cs, UICommand req) => null;

    UINode? IFixedXMLReceiver.CreateNode(FixedXMLHelper helper) => 
        new(new LocalXMLSRPGExamples.TileView(new(menu, index)), helper.CreateView(false));
}
}