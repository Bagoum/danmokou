using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Events;
using Danmokou.Core;
using Suzunoya.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <summary>
/// An XML menu with a single freeform group, to which
///  nodes representing on-screen targets can be dynamically added or removed.
/// <br/>When there are no added nodes, any inputs will result in a silent no-op.
/// </summary>
public class XMLDynamicMenu : UIController, IFreeformContainer {
    UIScreen IFreeformContainer.Screen => MainScreen;

    protected override bool CaptureFallthroughInteraction => false;
    protected override UIScreen?[] Screens => dynamicScreens.Prepend(MainScreen).ToArray();
    
    public UINode Unselect { get; private set; } = null!;
    private readonly List<UIScreen> dynamicScreens = new();

    public OverrideEvented<Func<UINode, ICursorState, UICommand, UIResult?>?> HandleDefaultUnselectNav { get; } = new(null);
    public UIFreeformGroup FreeformGroup { get; private set; } = null!;
    public override bool CanConsumeInput =>
        base.CanConsumeInput && (!inputFallthrough || QueuedInput != null || Current != Unselect 
                                  || LastOperationFrame == ETime.FrameNumber);

    public bool inputFallthrough = false;


    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }

    //most menus are defined in FirstFrame, but since XMLDynamicMenu is consumed as a service, it's useful
    // to construct it in Awake
    private void Awake() {
        Unselect = EmptyNode.MakeUnselector(DefaultUnselectorNav);
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => {
                //Allow clicks to fallthrough if they don't hit any nodes on this menu
                s.HTML.pickingMode = PickingMode.Ignore;
                s.Margin.SetLRMargin(0, 0);
            },
            UseControlHelper = false
        };

        FreeformGroup = new UIFreeformGroup(MainScreen, Unselect);
    }

    /// <summary>
    /// Create a UIScreen for this <see cref="XMLDynamicMenu"/>, as well as a <see cref="UIFreeformGroup"/> and an
    ///  unselector node within. Nodes or groups may be added to the freeform group by calling
    /// <see cref="UIFreeformGroup.AddNodeDynamic(UINode)"/> or <see cref="UIFreeformGroup.AddGroupDynamic"/>.
    /// <br/>By default, the screen does not allow fallthrough clicks
    /// (this can be configured by setting `HTML.pickingMode` in `<paramref name="builder"/>`).
    /// </summary>
    public (UIScreen, UIFreeformGroup) MakeScreen(Func<UINode, ICursorState, UICommand, UIResult?>? unselectNav, Action<UIScreen, VisualElement>? builder = null) {
        var s = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = builder ?? ((s, ve) => {
                //Block fallthrough clicks
                s.HTML.pickingMode = PickingMode.Position;
                s.Margin.SetLRMargin(0, 0);
            }),
            UseControlHelper = false
        };
        var gr = s.AddFreeformGroup(unselectNav);
        AddScreen(s);
        return (s, gr);
    }

    public void AddNodeDynamic(UINode n) {
        FreeformGroup.AddNodeDynamic(n);
    }

    public UIScreen AddScreen(UIScreen s) {
        dynamicScreens.Add(s);
        if (Built)
            BuildLate(s);
        return s;
    }

    public UIResult? DefaultUnselectorNav(UINode n, ICursorState cs, UICommand req) =>
        HandleDefaultUnselectNav.Value?.Invoke(n, cs, req);
}
}