using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.UIResult;

namespace Danmokou.UI.XML {
public record UIGroupHierarchy(UIGroup Group, UIGroupHierarchy? Parent) : IEnumerable<UIGroup> {
    public IEnumerator<UIGroup> GetEnumerator() {
        yield return Group;
        if (Parent != null)
            foreach (var g in Parent)
                yield return g;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{Group.Screen.Groups.IndexOf(Group)}.{Parent}";

    public static int Length(UIGroupHierarchy? g) => g == null ? 0 : 1 + Length(g.Parent);

    public bool IsStrictPrefix(UIGroupHierarchy? prefix) =>
        prefix == null || prefix == Parent || (Parent?.IsStrictPrefix(prefix) == true);

    public IEnumerable<UIGroup> StrictPrefixRemainder(UIGroupHierarchy? prefix) {
        if (Parent?.IsStrictPrefix(prefix) == true) {
            foreach (var p in Parent.StrictPrefixRemainder(prefix))
                yield return p;
            yield return Group;
        }
    }
    public IEnumerable<UIGroup> PrefixRemainder(UIGroupHierarchy? prefix) {
        if (this != prefix) {
            foreach (var p in Parent?.PrefixRemainder(prefix) ?? Array.Empty<UIGroup>())
                yield return p;
            yield return Group;
        }
    }

    public static UIGroupHierarchy? GetIntersection(UIGroupHierarchy? a, UIGroupHierarchy? b) {
        var diffLen = Length(a) - Length(b);
        for (;diffLen > 0; --diffLen)
            a = a!.Parent;
        for (; diffLen < 0; ++diffLen)
            b = b!.Parent;
        while (a != b) {
            a = a!.Parent;
            b = b!.Parent;
        }
        return a;
    }
}
public abstract class UIGroup {
    protected static UIResult NoOp => new StayOnNode(true);
    public bool Visible { get; private set; } = false;
    public UIScreen Screen { get; }
    public UIRenderSpace Render { get; }
    public UIGroup? Parent { get; set; }
    /// <summary>
    /// Groups that are directly dependent on this group for Show/Hide control.
    /// <br/>Such groups do not call Show/Hide on their own.
    /// </summary>
    public List<UIGroup> DependentGroups { get; } = new();
    private bool IsDependentGroup = false;
    public UIGroup DependentParent {
        set {
            IsDependentGroup = true;
            (Parent = value).DependentGroups.Add(this);
        }
    }
    public Func<IEnumerable<UINode?>>? LazyNodes { get; set; }
    private List<UINode> nodes;
    public List<UINode> Nodes {
        get {
            if (LazyNodes != null) {
                nodes = LazyNodes().FilterNone().ToList();
                Build(buildMap ?? throw new Exception("Lazy UIGroup realized too early"));
                LazyNodes = null;
            }
            return nodes;
        }
    }
    public bool Interactable { get; init; } = true;
    /// <summary>
    /// Node to go to when trying to enter this group.
    /// <br/>Note that this node may be in a different group.
    /// </summary>
    public UINode? EntryNodeOverride { get; set; }
    public Func<int>? EntryIndexOverride { get; init; }
    /// <summary>
    /// Node to go to when pressing the back key from within this group. Usually something like "return to menu".
    /// <br/>Note that this node may be in a different group.
    /// </summary>
    public UINode? ExitNodeOverride { get; init; }
    public int? ExitIndexOverride { get; init; }
    public Func<Task?>? OnLeave { private get; init; }
    public Func<Task?>? OnEnter { private get; init; }
    public Action<UIGroup>? OnScreenExitEnd { private get; init; }
    
    
    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIGroupHierarchy Hierarchy => new(this, Parent?.Hierarchy);
    public UIController Controller => Screen.Controller;
    public bool IsCurrent => Controller.Current != null && Nodes.Contains(Controller.Current);
    public UINode EntryNode {
        get {
            if (EntryNodeOverride != null)
                return EntryNodeOverride;
            if (EntryIndexOverride != null)
                return Nodes.ModIndex(EntryIndexOverride());
            return FirstInteractableNode;
        }
    }
    public UINode FirstInteractableNode {
        get {
            for (int ii = 0; ii < Nodes.Count; ++ii)
                if (Nodes[ii].AllowInteraction)
                    return Nodes[ii];
            throw new Exception("No valid interactable nodes for UIGroup");
        }
    }
    public UINode EntryNodeFromBottom {
        get {
            if (EntryNodeOverride != null)
                return EntryNodeOverride;
            if (EntryIndexOverride != null)
                return Nodes.ModIndex(EntryIndexOverride());
            for (int ii = Nodes.Count - 1; ii >= 0; --ii)
                if (Nodes[ii].AllowInteraction)
                    return Nodes[ii];
            throw new Exception("No valid entry nodes for UIGroup");
        }
    }
    public UINode? ExitNode => ExitNodeOverride ?? (ExitIndexOverride.Try(out var i) ? Nodes.ModIndex(i) : null);
    /// <summary>
    /// Since the entry node of a group may point elsewhere,
    /// a group can have an entry node without having any nodes at all.
    /// </summary>
    public bool HasEntryNode => EntryNodeOverride != null || HasInteractableNodes;
    public bool HasInteractableNodes => Interactable && Nodes.Any(n => n.AllowInteraction);
    
    public UIGroup(UIScreen container, UIRenderSpace? render, IEnumerable<UINode?> nodes) {
        Screen = container;
        Render = render ?? container.ColumnRender(0);
        this.nodes = nodes.FilterNone().ToList();
        Render.AddGroup(this);
        Screen.AddGroup(this);
    }
    public UIGroup(UIRenderSpace render, IEnumerable<UINode?> nodes) : this(render.Screen, render, nodes) { }

    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        foreach (var n in nodes)
            n.Group = this;
        foreach (var n in nodes)
            n.Build(map);
    }

    public void AddNodeDynamic(UINode n) {
        Nodes.Add(n);
        n.Group = this;
        if (buildMap != null)
            n.Build(buildMap);
        Controller.Redraw();
    }

    public void ClearNodes() {
        foreach (var n in Nodes.ToList())
            n.Remove();
        Nodes.Clear();
    }

    public void Destroy() {
        ClearNodes();
        Screen.Groups.Remove(this);
        Render.RemoveGroup(this);
        foreach (var g in DependentGroups)
            g.Destroy();
    }
    
    public void Show() {
        Render.HideAllGroups();
        Visible = true;
    }
    private void EnterShow(bool callIfDependent=false) {
        if (IsDependentGroup && !callIfDependent) return;
        Show();
        foreach (var g in DependentGroups)
            g.EnterShow(true);
    }

    public void Hide() {
        Visible = false;
    }
    private void LeaveHide(bool callIfDependent=false) {
        if (IsDependentGroup && !callIfDependent) return;
        Hide();
        foreach (var g in DependentGroups)
            g.LeaveHide(true);
    }

    /// <summary>
    /// When an enclosed group has a navigation command that moves out of the bounds of the group,
    ///  its enclosing group has the ability to determine the result.
    /// <br/>Eg. Pressing up on the second node in a column group navigates to the first; this function is not called.
    /// <br/>Eg. Pressing up on the first node in a column group calls this function. If the enclosing group
    ///  is also a column group, then it returns null here, and navigation wraps around to the last node.
    /// <br/>Eg. Pressing left on any node in a column group calls this function. If the enclosing group is also
    ///  a column group, it returns ReturnToGroupCaller.
    /// </summary>
    public abstract UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req);

    /// <summary>
    /// Overriding this function allows an enclosed group to not delegate navigation to its enclosure
    /// (useful for cases such as popups).
    /// </summary>
    protected virtual bool TryDelegateNavigationToEnclosure(UINode current, UICommand req, out UIResult res) {
        res = default!;
        return Parent != null && Parent.NavigateOutOfEnclosed(this, current, req).Try(out res);
    }

    protected UIResult NavigateToPreviousNode(UINode node, UICommand req) {
        var bInd = Nodes.IndexOf(node);
        var ii = bInd - 1;
        for (; ii != bInd; --ii) {
            if (ii == -1) {
                if (TryDelegateNavigationToEnclosure(node, req, out var res))
                    return res;
                ii = Nodes.Count - 1;
            }
            if (Nodes[ii].AllowInteraction)
                break;
        }
        return new GoToNode(this, ii);
    }

    protected UIResult NavigateToNextNode(UINode node, UICommand req) {
        var bInd = Nodes.IndexOf(node);
        var ii = bInd + 1;
        for (; ii != bInd; ++ii) {
            if (ii == Nodes.Count) {
                if (Parent != null && Parent.NavigateOutOfEnclosed(this, node, req).Try(out var res))
                    return res;
                ii = 0;
            }
            if (Nodes[ii].AllowInteraction)
                break;
        }
        return new GoToNode(this, ii);
    }

    protected UIResult? GoToShowHideGroupIfExists(UINode node) =>
        node.ShowHideGroup == null || !node.ShowHideGroup.HasInteractableNodes || !node.IsEnabled ?
            null :
            new GoToNode(node.ShowHideGroup, null);

    protected UIResult? GoToExitOrLeaveScreen(UINode current, UICommand req) =>
        (ExitNode != null && ExitNode != current) ?
            new GoToNode(ExitNode) :
        Parent?.NavigateOutOfEnclosed(this, current, req) ??
            (Controller.ScreenCall.Count > 0 ?
                new ReturnToScreenCaller() :
                null);

    /// <summary>
    /// Handle default navigation for UINodes.
    /// </summary>
    public abstract UIResult Navigate(UINode node, UICommand req);

    public virtual void EnteredNode(UINode node, bool animate) { }


    public virtual Task? EnterGroup() {
        EnterShow();
        Controller.Redraw();
        return OnEnter?.Invoke();
    }

    protected virtual Task LeaveGroupTasks() {
        return OnLeave?.Invoke() ?? Task.CompletedTask;
    }
    public Task LeaveGroup() => LeaveGroupTasks().ContinueWithSync(() => LeaveHide());
    
    public virtual void ScreenExitStart() { }
    public void ScreenExitEnd() {
        OnScreenExitEnd?.Invoke(this);
    }
    public virtual void ScreenEnterStart() { }
    
    public virtual void ScreenEnterEnd() { }
}

public class UIColumn : UIGroup {
    public UIColumn(UIRenderSpace render, params UINode?[] nodes) : 
        this(render.Screen, render, nodes as IEnumerable<UINode?>) { }
    public UIColumn(UIRenderSpace render, IEnumerable<UINode?> nodes) : 
        this(render.Screen, render, nodes) { }
    public UIColumn(UIScreen container, UIRenderSpace? render, params UINode?[] nodes) : 
        this(container, render, nodes as IEnumerable<UINode>) { }

    public UIColumn(UIScreen container, UIRenderSpace? render, IEnumerable<UINode?> nodes) : base(container, render,
        nodes) { }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) => req switch {
        UICommand.Left => new ReturnToGroupCaller(),
        UICommand.Back => new ReturnToGroupCaller(),
        _ => null
    };
    public override UIResult Navigate(UINode node, UICommand req) => req switch {
        UICommand.Left => Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Right => GoToShowHideGroupIfExists(node) ?? Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Up => NavigateToPreviousNode(node, req),
        UICommand.Down => NavigateToNextNode(node, req),
        UICommand.Confirm => GoToShowHideGroupIfExists(node) ?? NoOp,
        UICommand.Back => GoToExitOrLeaveScreen(node, req) ?? NoOp,
        _ => throw new ArgumentOutOfRangeException(nameof(req), req, null)
    };
}

public class UIRow : UIGroup {
    public UIRow(UIRenderSpace render, params UINode?[] nodes) : 
        this(render.Screen, render, nodes as IEnumerable<UINode?>) { }
    public UIRow(UIRenderSpace render, IEnumerable<UINode?> nodes) : 
        this(render.Screen, render, nodes) { }
    public UIRow(UIScreen container, UIRenderSpace? render, params UINode?[] nodes) : 
        this(container, render, nodes as IEnumerable<UINode>) { }

    public UIRow(UIScreen container, UIRenderSpace? render, IEnumerable<UINode?> nodes) : base(container, render,
        nodes) { }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) => req switch {
        UICommand.Up => new ReturnToGroupCaller(),
        UICommand.Down => new ReturnToGroupCaller(),
        _ => null
    };
    public override UIResult Navigate(UINode node, UICommand req) => req switch {
        UICommand.Up => Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Down => Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Left => NavigateToPreviousNode(node, req),
        UICommand.Right => NavigateToNextNode(node, req),
        UICommand.Confirm => GoToShowHideGroupIfExists(node) ?? NoOp,
        UICommand.Back => GoToExitOrLeaveScreen(node, req) ?? NoOp,
        _ => throw new ArgumentOutOfRangeException(nameof(req), req, null)
    };
}

public class PopupUIGroup : CompositeUIGroup {
    private UINode Source { get; }
    public PopupUINode Main { get; }
    private new UIRenderAbsoluteTerritory Render { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="main"></param>
    /// <param name="body"></param>
    /// <param name="options">Constructor for options. If the left options are null, creates a single Back button.</param>
    /// <returns></returns>
    public static PopupUIGroup LRB2(UINode source, PopupUINode main, Func<UIRenderSpace, UIGroup> body,
        Func<PopupUINode, (UINode[]? left, UINode[] right)> options) {
        var mainGroup = new UIColumn(source.Screen.AbsoluteTerritory, main);
        var bodyGroup = body(new UIRenderExplicit(source.Screen, _ => main.Body));
        var (leftOpts, rightOpts) = options(main);
        leftOpts ??= new UINode[] { UIButton.Back(source) };
        foreach (var n in leftOpts.FilterNone()) {
            n.BuildTarget = ve => ve.Q("Left");
        }
        foreach (var n in rightOpts.FilterNone()) {
            n.BuildTarget = ve => ve.Q("Right");
        }
        var opts = leftOpts.Concat(rightOpts).FilterNone().ToArray();
        var entry = opts.FirstOrDefault(o => o is UIButton { Type: UIButton.ButtonType.Confirm });
        var exit = opts.FirstOrDefault(o => o is UIButton { Type: UIButton.ButtonType.Cancel });
        return new PopupUIGroup(source, mainGroup, new VGroup(bodyGroup, 
            new UIRow(new UIRenderExplicit(source.Screen, _ => main.Options), opts) {
                EntryNodeOverride = entry,
                ExitNodeOverride = exit
            })) {
            EntryNodeOverride = bodyGroup.HasEntryNode ? (UINode?)bodyGroup.EntryNode : entry,
            ExitNodeOverride = exit
        };
    }
    
    public PopupUIGroup(UINode source, UIGroup main, UIGroup body) :
        //mainGroup uses AbsoluteTerritory
        base(source.Screen.DirectRender, new[] {main, body}) {
        this.Render = source.Screen.AbsoluteTerritory;
        this.Source = source;
        this.Main = main.Nodes[0] as PopupUINode ?? throw new Exception("Popup group was not provided a PopupUINode");
        Main.UseDefaultAnimations = false;
        Main.Passthrough = true;
        this.Parent = source.Group;
    }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) => req switch {
        UICommand.Back => Main.EasyExit ? Source.ReturnGroup : NoOp,
        _ => null
    };
    
    //Popups do not delegate navigation to the enclosure.
    protected override bool TryDelegateNavigationToEnclosure(UINode current, UICommand req, out UIResult res) {
        res = default!;
        return false;
    }

    public override Task EnterGroup() {
        Main.NodeHTML.transform.scale = new Vector3(1, 0, 0);
        return Task.WhenAll(base.EnterGroup() ?? Task.CompletedTask,
                Render.FadeIn(),
                Main.NodeHTML.transform.ScaleTo(new Vector3(1, 1, 1), 0.12f, Easers.EOutSine).Run(Controller));
    }

    protected override Task LeaveGroupTasks() {
        return Task.WhenAll(base.LeaveGroupTasks(), 
                Render.FadeOutIfNoOtherDependencies(Main.Group),
                Main.NodeHTML.transform.ScaleTo(new Vector3(1, 0, 1), 0.12f, Easers.EIOSine).Run(Controller))
                .ContinueWithSync(Destroy);
    }
}

public class CommentatorAxisColumn<T> : UIColumn {
    private readonly Dictionary<UINode, T> nodeToVal = new();
    public Commentator<T>? Commentator { get; init; }
    public Vector2 BaseLoc { get; init; } = new(-3.1f * 240f, 0);
    //Note that the y-axis is inverted in CSS
    public Vector2 Axis { get; init; } = new(0, .45f * 240f);
    private Cancellable? canceller;
    
    public CommentatorAxisColumn(UIScreen container, UIRenderSpace? render, (UINode?, T)[] nodes) :
        base(container, render, nodes.Select(n => n.Item1)) {
        foreach (var (n, v) in nodes) {
            if (n != null) {
                nodeToVal[n] = v;
                n.UseDefaultAnimations = false;
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
        var center = Nodes.Count / 2f;
        var selLoc = Axis * ((selectedIndex - center) * 0.8f);
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

/// <summary>
/// A UIGroup that is a wrapper around other UIGroups, and has no interactable nodes of its own.
/// </summary>
public abstract class CompositeUIGroup : UIGroup {
    public List<UIGroup> Groups { get; }
    public CompositeUIGroup(IReadOnlyList<UIGroup> groups) : this(new UIRenderDirect(groups[0].Screen), groups) { }
    public CompositeUIGroup(UIRenderSpace render, IEnumerable<UIGroup> groups, params UINode[] nodes) : base(render, nodes) {
        Groups = groups.ToList();
        foreach (var g in Groups) {
            g.DependentParent = this;
            EntryNodeOverride ??= g.EntryNodeOverride;
        }
    }

    protected void AddGroup(UIGroup g) {
        Groups.Add(g);
        g.DependentParent = this;
        EntryNodeOverride ??= g.EntryNodeOverride;
    }

    public sealed override UIResult Navigate(UINode node, UICommand req) =>
        throw new Exception("CompositeGroup has no nodes to navigate");

    
    private UIResult? NavigateInDirection(List<UIGroup> axis, bool forwards, UIGroup enclosed, UINode current, UICommand req, bool useBottomEntry=false) {
        var ind = axis.IndexOf(enclosed);
        if (((ind == 0 && !forwards) || (ind == axis.Count - 1 && forwards)) &&
            Parent != null && Parent.NavigateOutOfEnclosed(this, current, req).Try(out var res))
            return res;
        var step = forwards ? 1 : -1;
        for (int ii = ind + step; ii != ind; ii = M.Mod(axis.Count, ii + step)) {
            if (axis.ModIndex(ii).HasEntryNode)
                return useBottomEntry ? axis.ModIndex(ii).EntryNodeFromBottom : axis.ModIndex(ii).EntryNode;
        }
        return null;
    }

    protected UIResult? NavigateUp(List<UIGroup> topDown, UIGroup enclosed, UINode current, UICommand req) =>
        NavigateInDirection(topDown, false, enclosed, current, req, true);
    protected UIResult? NavigateDown(List<UIGroup> topDown, UIGroup enclosed, UINode current, UICommand req) =>
        NavigateInDirection(topDown, true, enclosed, current, req, false);
    protected UIResult? NavigateLeft(List<UIGroup> leftRight, UIGroup enclosed, UINode current, UICommand req) =>
        NavigateInDirection(leftRight, false, enclosed, current, req, false);
    protected UIResult? NavigateRight(List<UIGroup> leftRight, UIGroup enclosed, UINode current, UICommand req) =>
        NavigateInDirection(leftRight, true, enclosed, current, req, false);
}

/// <summary>
/// A UIGroup with many subgroups oriented vertically.
/// <br/>This group has no nodes.
/// </summary>
public class VGroup : CompositeUIGroup {

    public VGroup(params UIGroup[] groups) : base(groups) { }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) {
        var ind = Groups.IndexOf(enclosed);
        
        return req switch {
            UICommand.Up => NavigateUp(Groups, enclosed, current, req),
            UICommand.Down => NavigateDown(Groups, enclosed, current, req),
            UICommand.Back => GoToExitOrLeaveScreen(current, req),
            _ => null
        };
    }
}

/// <summary>
/// A UIGroup with three subgroups: a left, a right, and a vertical (bottom or top) group.
/// <br/>The left and right groups should sum to 100% width and the vertical group should sit on top or below the two.
/// <br/>This group has no nodes.
/// </summary>
public abstract class LRVGroup : CompositeUIGroup {
    public UIGroup Left { get; }
    public UIGroup Right { get; }
    public UIGroup Vert { get; }
    public abstract bool VertIsTop { get; }

    private List<UIGroup> VertAxis => VertIsTop ?
        new List<UIGroup> { Vert, DefaultLRGroup } :
        new() { DefaultLRGroup, Vert };

    private List<UIGroup> HorizAxis => new() { Left, Right };

    private bool lastExitedLROnLeft = true;
    private UIGroup DefaultLRGroup => lastExitedLROnLeft ? Left : Right;
    
    public LRVGroup(UIGroup left, UIGroup right, UIGroup vert) : base(new[]{left,right,vert}) {
        Left = left;
        Right = right;
        Vert = vert;
    }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) {
        if (req == UICommand.Back)
            return GoToExitOrLeaveScreen(current, req);
        if (enclosed == Left) {
            lastExitedLROnLeft = true;
            return req switch {
                UICommand.Up => NavigateUp(VertAxis, enclosed, current, req),
                UICommand.Down => NavigateDown(VertAxis, enclosed, current, req),
                UICommand.Right => NavigateRight(HorizAxis, enclosed, current, req),
                _ => null
            };
        } else if (enclosed == Right) {
            lastExitedLROnLeft = false;
            return req switch {
                UICommand.Up => NavigateUp(VertAxis, enclosed, current, req),
                UICommand.Down => NavigateDown(VertAxis, enclosed, current, req),
                UICommand.Right => NavigateLeft(HorizAxis, enclosed, current, req),
                _ => null
            };
        } else if (enclosed == Vert) {
            return req switch {
                UICommand.Up => NavigateUp(VertAxis, enclosed, current, req),
                UICommand.Down => NavigateDown(VertAxis, enclosed, current, req),
                _ => null
            };
        } else
            throw new Exception("LRVGroup should not be a parent to anything other than its LRV components");
    }

}

/// <summary>
/// A UIGroup with three subgroups: a left, a right, and a bottom group.
/// </summary>
public class LRBGroup : LRVGroup {
    public override bool VertIsTop => false;
    public LRBGroup(UIGroup left, UIGroup right, UIGroup vert) : base(left, right, vert) { }
}

}