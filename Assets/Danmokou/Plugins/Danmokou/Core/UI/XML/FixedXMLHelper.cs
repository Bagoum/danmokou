using System;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.UI.XML;
using MathNet.Numerics.LinearAlgebra.Single;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI {
/// <summary>
/// A class for MonoBehaviors that can receive events from UITK.
/// </summary>
public interface IFixedXMLReceiver {
    public LString? Tooltip => null;
    public void OnBuilt(UINode n, IFixedXMLObject cfg) { }
    public UIResult? Navigate(UINode n, ICursorState cs, UICommand req);
    public void OnEnter(UINode n, ICursorState cs) { }
    public void OnLeave(UINode n, ICursorState cs) { }
    public void OnPointerDown(UINode n, PointerDownEvent ev) { }
    public void OnPointerUp(UINode n, PointerUpEvent ev) { }

    /// <summary>
    /// If implemented, overrides the default UINode creation for FixedXMLHelper (which makes an empty node).
    /// </summary>
    public UINode? CreateNode(FixedXMLHelper helper) => null;
}

/// <summary>
/// A helper script that creates a <see cref="EmptyNode"/> and links its callback events
///  to a provided <see cref="IFixedXMLReceiver"/>.
/// </summary>
public class FixedXMLHelper : CoroutineRegularUpdater {
    //in unity units
    public Vector2 Size = Vector2.one;
    public Vector2 Offset;
    public bool keyboardNavigable = true;

    public WorldTrackingXML XML { get; private set; } = null!;
    public UINode Node { get; private set; } = null!;

    public IFixedXMLReceiver? Receiver { get; set; }

    public FixedXMLView CreateView(bool absPosition = true) => new(new(XML, Receiver)) {
        IsKeyboardNavigable = keyboardNavigable,
        IsAbsPositioned = absPosition
    };

    private void Awake() {
        XML = new(UIBuilderRenderer.UICamInfo, () => transform.position + (Vector3)Offset, () => Size);
    }
    
    public override void FirstFrame() {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Node is not null) return;
        ServiceLocator.Find<XMLDynamicMenu>().AddNodeDynamic(MakeNode());
    }

    public UINode MakeNode() {
        if (CameraRenderer.FindCapturer(1 << gameObject.layer).Try(out var camr))
            XML.TargetCam = camr.CamInfo;
        return Node = Receiver?.CreateNode(this) ?? new EmptyNode(CreateView());
    }

    protected override void OnDisable() {
        base.OnDisable();
        Node.Remove();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
        var position = (Vector2) transform.position;
        Handles.color = Color.green;
        Handles.DrawSolidRectangleWithOutline(new Rect((position + Offset) - Size / 2, Size),
            Color.clear, Color.green);
    }
#endif

}
}