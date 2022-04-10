using System;
using System.Collections.Generic;
using BagoumLib.Events;
using Danmokou.Core;
using Suzunoya.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public interface IFixedXMLObject {
    string Descriptor { get; }
    ICObservable<float> Left { get; }
    ICObservable<float> Top { get; }
    ICObservable<float> Width { get; }
    ICObservable<float> Height { get; }
    Evented<bool> IsVisible { get; }
    UIResult? Navigate(UINode n, UICommand c);
}

public record FixedXMLObject(float l, float t, float w = 100, float h = 100) : IFixedXMLObject {
    public string Descriptor { get; init; } = "";
    ICObservable<float> IFixedXMLObject.Left => Left;
    public Evented<float> Left { get; } = new(l);
    ICObservable<float> IFixedXMLObject.Top => Top;
    public Evented<float> Top { get; } = new(t);
    ICObservable<float> IFixedXMLObject.Width => Width;
    public Evented<float> Width { get; } = new(w);
    ICObservable<float> IFixedXMLObject.Height => Height;
    public Evented<float> Height { get; } = new(h);
    public Evented<bool> IsVisible { get; } = new(true);
    public Func<UINode, UIResult?>? OnConfirm { get; init; }
    public UIResult? Navigate(UINode n, UICommand c) => 
        c is UICommand.Confirm ? OnConfirm?.Invoke(n) : null;
}

/// <summary>
/// An XML menu that is visible and controllable at all times. It has a single freeform group,
///  and nodes representing on-screen targets can be dynamically added or removed.
/// <br/>When there are no added nodes, any inputs will result in a silent no-op.
/// </summary>
public class XMLPersistentInteractive : UIController {
    private class UnselectorFixedXML : IFixedXMLObject {
        public string Descriptor => "Unselector";
        public ICObservable<float> Top { get; } = new ConstantObservable<float>(0);
        public ICObservable<float> Left { get; } = new ConstantObservable<float>(0);
        public ICObservable<float> Width { get; } = new ConstantObservable<float>(0);
        public ICObservable<float> Height { get; } = new ConstantObservable<float>(0);
        public Evented<bool> IsVisible { get; } = new(true);
        public UIResult? Navigate(UINode n, UICommand c) => null;
    }
    private UINode unselect = null!;
    private UIFreeformGroup group = null!;
    private List<UINode>? _addNodeQueue = new();

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }

    public override void FirstFrame() {
        unselect = new EmptyNode(new UnselectorFixedXML());
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Builder = (s, ve) => {
                ve.AddColumn();
                s.Margin.SetLRMargin(0, 0);
            }
        };

        group = new UIFreeformGroup(MainScreen, unselect);
        base.FirstFrame();

        //Normally, the UI container will capture any pointer events not on nodes,
        //but for the persistent interactive menu, we want such events to fall through
        //to canvas/etc.
        UIRoot.pickingMode = PickingMode.Ignore;
        UIContainer.pickingMode = PickingMode.Ignore;
        
        if (_addNodeQueue != null)
            foreach (var n in _addNodeQueue)
                group.AddNodeDynamic(n);
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
            group.AddNodeDynamic(n);
    }
}
}