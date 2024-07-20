using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

/// <summary>
/// *Shared* view model for UI elements scrolling along an axis.
/// </summary>
public class AxisViewModel : UIViewModel {
    public Vector2 BaseLoc { get; init; } = new(-3.1f, 0);
    public Vector2 Axis { get; init; } = new(0, -.5f);
    public int Index { get; private set; }

    public void Set(UINode n) {
        Index = n.IndexInGroup;
    }
    
    public override long GetViewHash() => Index.GetHashCode();
}

public class AxisView : UIView<AxisViewModel>, IUIView {
    private Cancellable? canceller;
    public AxisView(AxisViewModel viewModel) : base(viewModel) {}

    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
        node.RootView.DisableAnimations();
    }

    public void OnEnter(UINode node, ICursorState cs, bool animate) {
        VM.Set(node);
    }

    public override void UpdateHTML() {
        SetRelativeLocation(VM.Index);
    }

    private void SetRelativeLocation(int selectedIndex) {
        var center = (Node.Group.Nodes.Count - 1) / 2f;
        var selLoc = VM.Axis * ((selectedIndex - center) * 1.4f);
        var nodeIndex = Node.IndexInGroup;
        var dist = Mathf.Abs(nodeIndex - selectedIndex);
        var effectiveDist = Mathf.Sign(nodeIndex - selectedIndex) * Mathf.Pow(dist, 0.6f);
        var myLoc = VM.BaseLoc + selLoc + VM.Axis * (effectiveDist * 4.2f);
        var isSel = nodeIndex == selectedIndex;
        var scale = isSel ? 2 : 1;
        var alpha = isSel ? 1 : 0.7f;
        var color = (Node.IsEnabled ? Color.white: Color.gray).WithA(alpha);
        var tr = HTML.transform;
        Cancellable.Replace(ref canceller);
        Node.Controller.PlayAnimation(Node.HTML.GoToLeftTop(
            UIBuilderRenderer.ToXMLOffset(myLoc), 0.6f, Easers.EOutSine, canceller));
        Node.Controller.PlayAnimation(tr.ScaleTo(scale, 0.4f, Easers.EOutSine, canceller));
        //sr.ColorTo(color, time, Easers.EOutSine, canceller).Run(this, new CoroutineOptions(true));
    }
}
}