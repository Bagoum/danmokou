using System;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.UI.XML;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI {
/// <summary>
/// A class for MonoBehaviors that can receive events from UITK.
/// </summary>
public interface IFixedXMLReceiver {
    public LString? Tooltip => null;
    public void OnBuilt(EmptyNode n) { }
    public UIResult OnConfirm(UINode n, ICursorState cs);
    public void OnEnter(UINode n, ICursorState cs);
    public void OnLeave(UINode n, ICursorState cs);
    public void OnPointerDown(UINode n, PointerDownEvent ev);
    public void OnPointerUp(UINode n, PointerUpEvent ev);
}

/// <summary>
/// A helper script that creates a <see cref="EmptyNode"/> and links its callback events
///  to a provided <see cref="IFixedXMLReceiver"/>.
/// </summary>
public class FixedXMLHelper : CoroutineRegularUpdater {
    public bool useVisibilityAsPassthrough = true;
    //in unity units
    public Vector2 Size = Vector2.one;
    public Vector2 Offset;

    public string[] xmlClasses = null!;
    public FixedXMLObject XML { get; private set; } = null!;
    public EmptyNode Node { get; private set; } = null!;

    public MonoBehaviour actionHandler = null!;
    
    public IFixedXMLObjectContainer? Container { get; set; }
    private IFixedXMLReceiver Receiver => 
        (actionHandler as IFixedXMLReceiver) ??
        throw new Exception($"Action handler {actionHandler} is not an IFixedXMLReceiver");

    public Vector2 XMLLocation => UIBuilderRenderer.ToXMLPos((Vector2)transform.position + Offset);
    public Vector2 XMLSize => UIBuilderRenderer.ToXMLDims(Size);

    private bool? delayedStartPassthrough = null;

    private void Awake() {
        XML = new(XMLLocation.x, XMLLocation.y, XMLSize.x, XMLSize.y) {
            Descriptor = gameObject.name
        };
        Node = new(XML, Receiver.OnBuilt, useVisibilityAsPassthrough) {
            OnConfirm = Receiver.OnConfirm,
            OnEnter = Receiver.OnEnter,
            OnLeave = Receiver.OnLeave,
            OnMouseDown = Receiver.OnPointerDown,
            OnMouseUp = Receiver.OnPointerUp
        };
        Node.WithCSS(xmlClasses);
    }
    public override void FirstFrame() {
        var menu = Container ?? ServiceLocator.Find<XMLDynamicMenu>();
        if (Receiver.Tooltip is { } tt)
            Node.PrepareTooltip(tt);
        menu.AddNodeDynamic(Node);
        if (delayedStartPassthrough != null)
            Node.UpdatePassthrough(delayedStartPassthrough);
    }

    protected override void OnDisable() {
        base.OnDisable();
        Node.Remove();
    }

    public void UpdateNodeIsEnabled(bool enable) {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        if (Node?.Built is true)
            Node.UpdatePassthrough(!enable);
        else
            delayedStartPassthrough = !enable;
    }

    [ContextMenu("Update locations")]
    public void UpdatedLocations() {
        var l = XMLLocation;
        var s = XMLSize;
        XML.Left.Value = l.x;
        XML.Top.Value = l.y;
        XML.Width.Value = s.x;
        XML.Height.Value = s.y;
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