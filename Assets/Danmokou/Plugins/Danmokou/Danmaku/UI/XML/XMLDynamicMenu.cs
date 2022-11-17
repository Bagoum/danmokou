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
    private List<UINode>? _addNodeQueue = new();
    private List<UIScreen> dynamicScreens = new();
    
    public Func<UINode, UIResult?>? HandleUnselectConfirm { get; set; }
    public UIFreeformGroup FreeformGroup { get; private set; } = null!;


    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }

    public override void FirstFrame() {
        Unselect = new EmptyNode(new UnselectorFixedXML()) {
            OnConfirm = UnselectorConfirm
        };
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => {
                //TODO for the other screens, you can set picking mode to capture fallthrough
                //s.HTML.pickingMode = PickingMode.Position;
                s.HTML.Q("ControlsHelper").RemoveFromHierarchy();
                //ve.AddColumn();
                s.Margin.SetLRMargin(0, 0);
            }
        };

        FreeformGroup = new UIFreeformGroup(MainScreen, Unselect);
        base.FirstFrame();

        if (!CaptureFallthroughInteraction) {
            //Normally, the UI container will capture any pointer events not on nodes,
            //but for the persistent interactive menu, we want such events to fall through
            //to canvas/etc.
            UIRoot.pickingMode = PickingMode.Ignore;
            UIContainer.pickingMode = PickingMode.Ignore;
        }
        
        if (_addNodeQueue != null)
            foreach (var n in _addNodeQueue)
                FreeformGroup.AddNodeDynamic(n);
        _addNodeQueue = null;
        
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

    public void AddNode(UINode n) {
        if (_addNodeQueue != null)
            _addNodeQueue.Add(n);
        else
            FreeformGroup.AddNodeDynamic(n);
    }

    public void AddScreen(UIScreen s) {
        dynamicScreens.Add(s);
        if (_addNodeQueue == null) // initial building has completed
            BuildLate(s);
    }

    protected UIResult UnselectorConfirm(UINode n) => 
        HandleUnselectConfirm?.Invoke(n) ?? 
        new UIResult.StayOnNode(UIResult.StayOnNodeType.Silent);
}
}