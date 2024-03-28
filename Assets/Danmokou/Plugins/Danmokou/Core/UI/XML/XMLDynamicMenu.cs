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
public class XMLDynamicMenu : UIController, IFixedXMLObjectContainer {
    UIScreen IFixedXMLObjectContainer.Screen => MainScreen;
    private class UnselectorFixedXML : IFixedXMLObject {
        public string Descriptor => "Unselector";
        public ICObservable<float> Top { get; } = new ConstantObservable<float>(0);
        public ICObservable<float> Left { get; } = new ConstantObservable<float>(0);
        public ICObservable<float?> Width { get; } = new ConstantObservable<float?>(0);
        public ICObservable<float?> Height { get; } = new ConstantObservable<float?>(0);
        public ICObservable<bool> IsVisible { get; } = new ConstantObservable<bool>(true);
        public UIResult? Navigate(UINode n, UICommand c) => null;
    }

    protected virtual bool CaptureFallthroughInteraction => false;
    protected override UIScreen?[] Screens => dynamicScreens.Prepend(MainScreen).ToArray();
    
    public UINode Unselect { get; private set; } = null!;
    private readonly List<UIScreen> dynamicScreens = new();
    
    public Func<UINode, UIResult?>? HandleUnselectConfirm { get; set; }
    public UIFreeformGroup FreeformGroup { get; private set; } = null!;


    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }

    //most menus are defined in FirstFrame, but since XMLDynamicMenu is consumed as a service, it's useful
    // to construct it in Awake
    private void Awake() {
        Unselect = new EmptyNode(new UnselectorFixedXML()) {
            OnConfirm = UnselectorConfirm
        };
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
    /// <see cref="UIFreeformGroup.AddNodeDynamic"/> or <see cref="UIFreeformGroup.AddGroupDynamic"/>.
    /// <br/>By default, the screen does not allow fallthrough clicks
    /// (this can be configured by setting `HTML.pickingMode` in `<paramref name="builder"/>`).
    /// </summary>
    public (UIScreen, UIFreeformGroup) MakeScreen(Func<UINode, UIResult?>? unselectConfirm, Action<UIScreen, VisualElement>? builder = null) {
        var unselect = new EmptyNode(new UnselectorFixedXML()) {
            OnConfirm = (n, cs) => unselectConfirm?.Invoke(n) ?? UIGroup.SilentNoOp
        };
        var s = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = builder ?? ((s, ve) => {
                //Block fallthrough clicks
                s.HTML.pickingMode = PickingMode.Position;
                s.Margin.SetLRMargin(0, 0);
            }),
            UseControlHelper = false
        };
        var gr = new UIFreeformGroup(s, unselect);
        AddScreen(s);
        return (s, gr);
    }

    public override void FirstFrame() {
        base.FirstFrame();
        
        if (!CaptureFallthroughInteraction) {
            //Normally, the UI container will capture any pointer events not on nodes,
            //but for the persistent interactive menu, we want such events to fall through
            //to canvas/etc.
            UIRoot.pickingMode = PickingMode.Ignore;
            UIContainer.pickingMode = PickingMode.Ignore;
        }

        //testing
        /*
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(500, 0) {
            OnConfirm = n => {
                Logs.Log("hello");
                return new UIResult.StayOnNode();
            }
        }));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(1900, 40) {
            OnConfirm = n => {
                Logs.Log("world");
                return new UIResult.StayOnNode();
            }
        }));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(3200, 80)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(600, 900)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(1500, 1000)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(3300, 1050)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(300, 1900)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(1900, 1700)));
        g.AddNodeDynamic(new EmptyNode(new FixedXMLObject(3400, 1800)));*/

    }

    public void AddNodeDynamic(UINode n) {
        FreeformGroup.AddNodeDynamic(n);
    }

    public void AddScreen(UIScreen s) {
        dynamicScreens.Add(s);
        if (Built)
            BuildLate(s);
    }

    protected UIResult UnselectorConfirm(UINode n, ICursorState _) =>
        HandleUnselectConfirm?.Invoke(n) ??
        UIGroup.SilentNoOp;
}
}