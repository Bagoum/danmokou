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
        public ICObservable<bool> IsInteractable { get; } = new ConstantObservable<bool>(true);
    }

    protected override bool CaptureFallthroughInteraction => false;
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
        Unselect = new EmptyNode(new FixedXMLView(new(new UnselectorFixedXML())
                { OnConfirmer = UnselectorConfirm }) 
            { AsEmpty = true });
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
        var unselect = 
            new EmptyNode(new FixedXMLView(new(new UnselectorFixedXML())
                    { OnConfirmer = (n, cs) => unselectConfirm?.Invoke(n) ?? UIGroup.SilentNoOp }) 
                { AsEmpty = true });
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

    public void AddNodeDynamic(UINode n) {
        FreeformGroup.AddNodeDynamic(n);
    }

    public UIScreen AddScreen(UIScreen s) {
        dynamicScreens.Add(s);
        if (Built)
            BuildLate(s);
        return s;
    }

    protected UIResult UnselectorConfirm(UINode n, ICursorState _) =>
        HandleUnselectConfirm?.Invoke(n) ??
        UIGroup.SilentNoOp;
}
}