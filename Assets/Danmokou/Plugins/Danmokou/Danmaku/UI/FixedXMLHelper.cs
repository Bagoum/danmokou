using System;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.UI.XML;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace Danmokou.UI {
public interface IFixedXMLReceiver {
    public void OnBuilt(EmptyNode n) { }
    public UIResult OnConfirm();
    public void OnEnter(UINode n);
    public void OnLeave(UINode n);
    public void OnPointerDown(UINode n, PointerDownEvent ev);
    public void OnPointerUp(UINode n, PointerUpEvent ev);
}
public class FixedXMLHelper : CoroutineRegularUpdater {
    //in unity units
    public Vector2 Size = Vector2.one;
    public Vector2 Offset;

    public string[] xmlClasses = null!;
    public FixedXMLObject XML { get; private set; } = null!;
    private EmptyNode Node { get; set; } = null!;

    public MonoBehaviour actionHandler = null!;
    private IFixedXMLReceiver Receiver => 
        (actionHandler as IFixedXMLReceiver) ??
        throw new Exception($"Action handler {actionHandler} is not an IFixedXMLReceiver");

    public Vector2 XMLLocation => UIBuilderRenderer.ComputeXMLPosition((Vector2)transform.position + Offset);
    public Vector2 XMLSize => UIBuilderRenderer.ComputeXMLDimensions(Size);

    private void Awake() {
        XML = new(XMLLocation.x, XMLLocation.y, XMLSize.x, XMLSize.y) {
            Descriptor = gameObject.name
        };
        MakeNode();
    }
    public override void FirstFrame() {
        ServiceLocator.Find<XMLPersistentInteractive>().AddNode(Node);
    }
    
    private void MakeNode() {
        Node = new(XML, Receiver.OnBuilt) {
            OnConfirm = Receiver.OnConfirm,
            OnEnter = Receiver.OnEnter,
            OnLeave = Receiver.OnLeave,
            OnMouseDown = Receiver.OnPointerDown,
            OnMouseUp = Receiver.OnPointerUp
        };
        Node.With(xmlClasses);
    }
    

    protected override void OnDisable() {
        base.OnDisable();
        Node.Remove();
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