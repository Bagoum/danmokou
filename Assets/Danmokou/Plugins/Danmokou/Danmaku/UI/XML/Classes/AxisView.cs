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

public class AxisViewModel : UIViewModel {
    public Vector2 BaseLoc { get; init; } = new(-3.1f, 0);
    public Vector2 Axis { get; init; } = new(0, -.5f);
    public int Index { get; private set; }

    public void Set(UINode n) {
        Index = n.IndexInGroup;
    }
    
    public override long GetViewHash() => Index.GetHashCode();
}

public class AxisView : UIView<AxisViewModel> {
    private Cancellable? canceller;
    public AxisView(AxisViewModel viewModel) : base(viewModel) {}

    public override void NodeBuilt(UINode node) {
        base.NodeBuilt(node);
        Node.DisableAnimations();
        Node.OnEnter = Node.OnEnter.Then((n, cs) => VM.Set(n));
    }

    protected override BindingResult Update(in BindingContext context) {
        SetRelativeLocation(VM.Index, !IsFirstRender());
        return base.Update(in context);
    }

    private void SetRelativeLocation(int selectedIndex, bool animate) {
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
        float time = 0.4f;
        var tr = Node.NodeHTML.transform;
        canceller?.Cancel();
        canceller = new Cancellable();
        if (!animate) {
            Node.HTML.WithAbsolutePosition(UIBuilderRenderer.ToXMLOffset(myLoc));
            //tr.position = myLoc;
            tr.scale = new Vector3(scale, scale, scale);
            //tr.color = color;
        } else {
            Node.Controller.PlayAnimation(Node.HTML.GoToLeftTop(
                UIBuilderRenderer.ToXMLOffset(myLoc), time, Easers.EOutSine, canceller));
            Node.Controller.PlayAnimation(tr.ScaleTo(scale, time, Easers.EOutSine, canceller));
            //sr.ColorTo(color, time, Easers.EOutSine, canceller).Run(this, new CoroutineOptions(true));
        }
    }
}
}