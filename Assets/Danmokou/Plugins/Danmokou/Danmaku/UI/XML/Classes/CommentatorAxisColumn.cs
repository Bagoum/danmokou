using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.UI.XML {

public class CommentatorAxisColumn<T> : UIColumn {
    private readonly Dictionary<UINode, T> nodeToVal = new();
    public Commentator<T>? Commentator { get; init; }
    public Vector2 BaseLoc { get; init; } = new(-3.1f * 240f, 0);
    //Note that the y-axis is inverted in CSS
    public Vector2 Axis { get; init; } = new(0, .5f * 240f);
    private Cancellable? canceller;
    
    public CommentatorAxisColumn(UIScreen container, UIRenderSpace? render, (UINode?, T)[] nodes) :
        base(container, render, nodes.Select(n => n.Item1)) {
        foreach (var (n, v) in nodes) {
            if (n != null) {
                nodeToVal[n.DisableAnimations()] = v;
            }
        }
    }
    public override UIResult Navigate(UINode node, UICommand req) => base.Navigate(node, req switch {
        UICommand.Left => UICommand.Up,
        UICommand.Right => UICommand.Down,
        _ => req
    });

    public override void EnteredNode(UINode node, bool animate) {
        base.EnteredNode(node, animate);
        if (Commentator != null)
            Commentator.SetCommentFromValue(nodeToVal[node]);
        canceller?.Cancel();
        canceller = new Cancellable();
        var selIndex = Nodes.IndexOf(node);
        foreach (var (i, n) in Nodes.Enumerate()) {
            SetRelativeLocation(i, selIndex, animate);
        }
    }

    private void SetRelativeLocation(int nodeIndex, int selectedIndex, bool animate) {
        var node = Nodes[nodeIndex];
        var center = (Nodes.Count - 1) / 2f;
        var selLoc = Axis * ((selectedIndex - center) * 1.4f);
        var dist = Mathf.Abs(nodeIndex - selectedIndex);
        var effectiveDist = Mathf.Sign(nodeIndex - selectedIndex) * Mathf.Pow(dist, 0.6f);
        var myLoc = BaseLoc + selLoc + Axis * (effectiveDist * 4.2f);
        var isSel = nodeIndex == selectedIndex;
        var scale = isSel ? 2 : 1;
        var alpha = isSel ? 1 : 0.7f;
        var color = (node.IsEnabled ? Color.white: Color.gray).WithA(alpha);
        float time = 0.4f;
        var tr = node.NodeHTML.transform;
        if (!animate) {
            tr.position = myLoc;
            tr.scale = new Vector3(scale, scale, scale);
            //tr.color = color;
        } else {
            tr.GoTo(myLoc, time, M.EOutSine, canceller).Run(Controller, new CoroutineOptions(true));
            tr.ScaleTo(scale, time, M.EOutSine, canceller).Run(Controller, new CoroutineOptions(true));
            //sr.ColorTo(color, time, M.EOutSine, canceller).Run(this, new CoroutineOptions(true));
        }

    }

    public override void ScreenExitStart() {
        base.ScreenExitStart();
        if (Commentator != null)
            Commentator.Disappear();
    }
    public override void ScreenEnterStart() {
        base.ScreenExitStart();
        if (Commentator != null)
            Commentator.Appear();
    }
}

}