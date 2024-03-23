using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
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
    public static readonly UIResult NoOp = new StayOnNode(true);
    public static readonly UIResult SilentNoOp = new StayOnNode(StayOnNodeType.Silent);
    public bool Visible { get; private set; } = false;
    public UIScreen Screen { get; }
    public UIRenderSpace Render { get; }
    
    /// <summary>
    /// The UI group that contains this UI group, and to which navigation delegates if internal navigation fails.
    /// <br/>Note that the parent does not neccessarily know about the child's existence,
    ///  unless it is set via <see cref="DependentParent"/>.
    /// </summary>
    public UIGroup? Parent { get; set; }
    
    /// <summary>
    /// Groups that should share the same display behavior as this group; ie.
    ///  if this group is visible, then listed groups here should be visible.
    /// <br/>Used by composite groups and show/hide handling in Node.
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
                foreach (var n in nodes)
                    n.Group = this;
                Build(buildMap ?? throw new Exception("Lazy UIGroup realized too early"));
                LazyNodes = null;
            }
            return nodes;
        }
    }
    public bool Interactable { get; init; } = true;
    /// <summary>
    /// Node to go to when trying to enter this group.
    /// <br/>Note that this node may be in a descendant of this group.
    /// </summary>
    public UINode? EntryNodeOverride { get; set; }
    public UINode? EntryNodeBottomOverride { get; set; }
    public Func<int>? EntryIndexOverride { get; init; }
    /// <summary>
    /// Node to go to when pressing the back key from within this group. Usually something like "return to menu".
    /// <br/>Note that this node may be in a descendant of this group.
    /// </summary>
    public UINode? ExitNodeOverride { get; set; }
    public int? ExitIndexOverride { get; init; }
    public Func<UIGroup, Task?>? OnEnter { private get; init; }
    public Func<UIGroup, Task?>? OnLeave { private get; init; }
    /// <inheritdoc cref="ScreenExitEnd"/>
    public Action<UIGroup>? OnScreenExitEnd { private get; init; }
    
    
    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIGroupHierarchy Hierarchy => new(this, Parent?.Hierarchy);
    public UIController Controller => Screen.Controller;
    public bool IsCurrent => Controller.Current != null && Nodes.Contains(Controller.Current);
    public UINode? PreferredEntryNode {
        get {
            if (EntryNodeOverride != null)
                return EntryNodeOverride;
            if (EntryIndexOverride != null)
                return Nodes.ModIndex(EntryIndexOverride());
            return null;
        }
    }
    public UINode EntryNode => PreferredEntryNode ?? FirstInteractableNode;
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
            if (EntryNodeBottomOverride != null)
                return EntryNodeBottomOverride;
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

    /// <summary>
    /// All nodes contained within this group, or within any groups that are descendants of this group
    /// (including show-hide and composite groups, but not popups to save effort on dynamicity handling)
    /// </summary>
    public virtual IEnumerable<UINode> NodesAndDependentNodes {
        get {
            foreach (var n in Nodes) {
                yield return n;
                if (n.ShowHideGroup != null)
                    foreach (var sn in n.ShowHideGroup.NodesAndDependentNodes)
                        yield return sn;
            }
        }
    }

    public UIGroup(UIScreen container, UIRenderSpace? render, IEnumerable<UINode?>? nodes) {
        Screen = container;
        Render = render ?? container.ColumnRender(0);
        this.nodes = nodes?.FilterNone().ToList() ?? new();
        foreach (var n in this.nodes)
            n.Group = this;
        Render.AddSource(this);
        Screen.AddGroup(this);
    }

    public UIGroup(UIRenderSpace render, IEnumerable<UINode?>? nodes) : this(render.Screen, render, nodes) { }

    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
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
        Screen.Groups.Remove(this);
        Render.RemoveSource(this);
        foreach (var g in DependentGroups)
            g.Destroy();
        ClearNodes();
    }


    /// <summary>
    /// Make the group visible (it is being entered).
    /// </summary>
    /// <param name="callIfDependent">If false, this will noop for dependent groups
    /// (their enterShow should be controlled by the parent).</param>
    public void EnterShow(bool callIfDependent = false) {
        if (IsDependentGroup && !callIfDependent) return;
        Visible = true;
        Render.SourceBecameVisible(this);
        EnterShowDependents();
    }

    protected virtual void EnterShowDependents() {
        foreach (var g in DependentGroups)
            g.EnterShow(true);
    }

    /// <summary>
    /// Make the group invisible (it is being left).
    /// </summary>
    /// <param name="callIfDependent">If false, this will noop for dependent groups
    /// (their leaveHide should be controlled by the parent).</param>
    public void LeaveHide(bool callIfDependent = false) {
        if (IsDependentGroup && !callIfDependent) return;
        Visible = false;
        Render.SourceBecameHidden(this);
        foreach (var g in DependentGroups)
            g.LeaveHide(true);
    }

    /// <summary>
    /// Handle any redraws for linked render groups.
    /// Occurs before component node redraws.
    /// </summary>
    public virtual void Redraw() { }

    /// <summary>
    /// When an group A has a navigation command that moves out of the bounds of A, this function is called on A.Parent to determine how to handle it.
    /// <br/>Eg. Pressing up on the second node in a column group navigates to the first; this function is not called.
    /// <br/>Eg. Pressing up on the first node in a column group calls this function. If the enclosing group
    ///  is also a column group, then it returns null here, and navigation wraps around to the last node.
    /// <br/>Eg. Pressing left on any node in a column group calls this function. If the enclosing group is also
    ///  a column group, it returns ReturnToGroupCaller.
    /// </summary>
    public abstract UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req);

    /// <summary>
    /// If permitted, delegate navigation to the parent group by calling <see cref="Parent"/>.<see cref="NavigateOutOfEnclosed"/>.
    /// <br/>Overrides such as <see cref="PopupUIGroup"/> may disable this if they do not want to permit delegation to parent group.
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
                if (TryDelegateNavigationToEnclosure(node, req, out var res))
                    return res;
                ii = 0;
            }
            if (Nodes[ii].AllowInteraction)
                break;
        }
        return new GoToNode(this, ii);
    }

    protected UIResult? GoToShowHideGroupIfExists(UINode node, UICommand dir) {
        if (node.ShowHideGroup == null || !node.IsEnabled) return null;
        if (node.ShowHideGroup.HasEntryNode) return node.ShowHideGroup.EntryNode;
        if (UIFreeformGroup.FindClosest(node.WorldLocation, dir, node.ShowHideGroup.NodesAndDependentNodes,
                   CompositeUIGroup._angleLimits, x => x != node) is { } n)
            return n;
        return null;
    }

    protected UIResult? GoToShowHideGroupFromBelowIfExists(UINode node, UICommand dir) {
        if (node.ShowHideGroup == null || !node.IsEnabled) return null;
        if (node.ShowHideGroup.HasEntryNode) return node.ShowHideGroup.EntryNodeFromBottom;
        if (UIFreeformGroup.FindClosest(node.WorldLocation, dir, node.ShowHideGroup.NodesAndDependentNodes,
            CompositeUIGroup._angleLimits, x => x != node) is { } n)
            return n;
        return null;
    }

    protected UIResult? GoToExitOrLeaveScreen(UINode current, UICommand req) =>
        (ExitNode != null && ExitNode != current) ?
            new GoToNode(ExitNode) :
        Parent?.NavigateOutOfEnclosed(this, current, req) ??
            (Controller.ScreenCall.Count > 0 && Screen.AllowsPlayerExit ?
                new ReturnToScreenCaller() :
                null);

    /// <summary>
    /// When a node N has no specific handle for a navigation command, this function is called on N.Group to handle default navigation.
    /// </summary>
    public abstract UIResult Navigate(UINode node, UICommand req);

    public virtual void EnteredNode(UINode node, bool animate) { }


    public virtual Task? EnterGroup() {
        EnterShow();
        return OnEnter?.Invoke(this);
    }

    protected virtual Task LeaveGroupTasks() {
        return OnLeave?.Invoke(this) ?? Task.CompletedTask;
    }
    public Task LeaveGroup() => LeaveGroupTasks().ContinueWithSync(() => LeaveHide());
    
    public virtual void ScreenExitStart() { }
    
    /// <summary>
    /// Called by the containing screen after it has transitioned to a different screen.
    /// </summary>
    public void ScreenExitEnd() {
        OnScreenExitEnd?.Invoke(this);
    }
    public virtual void ScreenEnterStart() { }
    
    public virtual void ScreenEnterEnd() { }
    
    public override string ToString() => $"{Hierarchy}({this.GetType()})";
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
        UICommand.Right => GoToShowHideGroupIfExists(node, req) ?? Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Up => NavigateToPreviousNode(node, req),
        UICommand.Down => NavigateToNextNode(node, req),
        UICommand.Confirm => GoToShowHideGroupIfExists(node, req) ?? NoOp,
        UICommand.Back => GoToExitOrLeaveScreen(node, req) ?? NoOp,
        _ => throw new ArgumentOutOfRangeException(nameof(req), req, null)
    };
}

public class UIRow : UIGroup {
    public bool ShowHideDownwards { get; set; } = true;
    public bool ShowHideUpwards { get; set; } = true;
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
        UICommand.Back => new ReturnToGroupCaller(),
        _ => null
    };
    public override UIResult Navigate(UINode node, UICommand req) => req switch {
        UICommand.Up => (ShowHideUpwards ? GoToShowHideGroupFromBelowIfExists(node, req) : null) ?? 
                        Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Down => (ShowHideDownwards ? GoToShowHideGroupIfExists(node, req) : null) 
                          ?? Parent?.NavigateOutOfEnclosed(this, node, req) ?? NoOp,
        UICommand.Left => NavigateToPreviousNode(node, req),
        UICommand.Right => NavigateToNextNode(node, req),
        UICommand.Confirm => GoToShowHideGroupIfExists(node, req) ?? NoOp,
        UICommand.Back => GoToExitOrLeaveScreen(node, req) ?? NoOp,
        _ => throw new ArgumentOutOfRangeException(nameof(req), req, null)
    };
}

public record PopupButtonOpts {
    public record LeftRightFlush(UINode?[]? left, UINode?[] right) : PopupButtonOpts {
        public static LeftRightFlush Default { get; } = new(null, Array.Empty<UINode>());
    }

    public record Centered(UINode?[]? options): PopupButtonOpts;
}

public class PopupUIGroup : CompositeUIGroup {
    private UINode Source { get; }
    
    public bool EasyExit { get; set; }
    public float? OverlayAlphaOverride { get; set; }
    private readonly Func<LString>? header;
    private readonly UIRenderConstructed render;

    /// <summary>
    /// Create a popup with a row of action buttons at the bottom.
    /// </summary>
    /// <param name="source">The node that spawned the popup</param>
    /// <param name="header">Popup header (optional)</param>
    /// <param name="bodyInner">Constructor for the UIGroup containing the popup messages, entry box, etc</param>
    /// <param name="buttons">Configuration for action buttons</param>
    /// <returns></returns>
    public static PopupUIGroup CreatePopup(UINode source, Func<LString>? header, Func<UIRenderSpace, UIGroup> bodyInner,
        PopupButtonOpts buttons) {
        var render = MakeRenderer(source.Screen.AbsoluteTerritory, XMLUtils.Prefabs.Popup);
        var bodyGroup = bodyInner(new UIRenderExplicit(source.Screen, _ => render.HTML.Q("BodyHTML")));
        UINode?[] opts;
        if (buttons is PopupButtonOpts.LeftRightFlush lr) {
            var leftOpts = lr.left ?? new UINode[] { UIButton.Back(source) };
            foreach (var n in leftOpts.FilterNone()) 
                n.BuildTarget = ve => ve.Q("Left");
            foreach (var n in lr.right.FilterNone()) 
                n.BuildTarget = ve => ve.Q("Right");
            opts = leftOpts.Concat(lr.right).ToArray();
        } else if (buttons is PopupButtonOpts.Centered c) {
            opts = c.options ?? new UINode[] { UIButton.Back(source) };
            foreach (var n in opts.FilterNone())
                n.BuildTarget = ve => ve.Q("Center");
        } else throw new NotImplementedException();
        var exit = opts.FirstOrDefault(o => o is UIButton { Type: UIButton.ButtonType.Cancel });
        var entry = opts.FirstOrDefault(o => o is UIButton { Type: UIButton.ButtonType.Confirm }) ?? exit;
        var p = new PopupUIGroup(render, header, source, new VGroup(bodyGroup, 
            new UIRow(new UIRenderExplicit(source.Screen, _ => render.HTML.Q("OptionsHTML")), opts) {
                EntryNodeOverride = entry,
                ExitNodeOverride = exit
            })) {
            EasyExit = opts.Any(o => o is UIButton { Type: UIButton.ButtonType.Cancel}),
            EntryNodeOverride = bodyGroup.HasEntryNode ? (UINode?)bodyGroup.EntryNode : entry,
            ExitNodeOverride = exit
        };
        return p;
    }

    public static PopupUIGroup CreateContextMenu(UINode source, UINode?[] options) {
        var render = MakeRenderer(source.Screen.AbsoluteTerritory, XMLUtils.Prefabs.ContextMenu);
        render.HTML.ConfigureAbsolute(XMLUtils.Pivot.TopLeft).WithAbsolutePosition(
            source.HTML.worldBound.center + source.HTML.worldBound.size * 0.4f);
        var back = new FuncNode(LocalizedStrings.Generic.generic_back, UIButton.GoBackCommand<FuncNode>(source));
        //NB: you can press X/RightClick *once* to leave an options menu.
        // If you add a ExitNodeOverride to the UIColumn, then you'll need to press it twice (as with standard popups)
        var grp = new UIColumn(new UIRenderConstructed(render, new(XMLUtils.AddColumn)), 
            options.Append(back).Select(x => x?.With(XMLUtils.noPointerClass)));
        var p = new PopupUIGroup(render, null, source, grp) {
            EntryNodeOverride = options[0],
            ExitNodeOverride = back,
            EasyExit = true,
            OverlayAlphaOverride = 0,
        };
        return p;
    }

    private static UIRenderConstructed MakeRenderer(UIRenderAbsoluteTerritory at, VisualTreeAsset prefab) {
        var render = new UIRenderConstructed(at, prefab);
        //Don't allow pointer events to hit the underlying Absolute Territory
        render.HTML.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
        return render;
    }
    
    public PopupUIGroup(UIRenderConstructed r, Func<LString>? header, UINode source, UIGroup body) :
        base(r, body) {
        this.render = r;
        this.header = header;
        this.Source = source;
        this.Parent = source.Group;
    }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) => req switch {
        UICommand.Back => EasyExit ? Source.ReturnGroup : NoOp,
        _ => null
    };
    
    //Popups do not delegate navigation to the enclosure.
    protected override bool TryDelegateNavigationToEnclosure(UINode current, UICommand req, out UIResult res) {
        res = default!;
        return false;
    }

    //note: we don't handle these tasks in UIRenderConstructed since UIRenderConstructed creates nonblocking tasks;
    // these are blocking.
    public override Task EnterGroup() {
        render.HTML.transform.scale = new Vector3(1, 0, 0);
        return Task.WhenAll(base.EnterGroup() ?? Task.CompletedTask,
                Screen.AbsoluteTerritory.FadeIn(),
                render.HTML.transform.ScaleTo(new Vector3(1, 1, 1), 0.12f, Easers.EOutSine).Run(Controller));
    }

    protected override Task LeaveGroupTasks() {
        return Task.WhenAll(base.LeaveGroupTasks(), 
                Screen.AbsoluteTerritory.FadeOutIfNoOtherDependencies(this),
                render.HTML.transform.ScaleTo(new Vector3(1, 0, 1), 0.12f, Easers.EIOSine).Run(Controller))
                .ContinueWithSync(() => {
                    Destroy();
                    render.Destroy();
                });
    }

    public override void Redraw() {
        var h = Render.HTML.Q<Label>("Header");
        if (header is null) {
            if (h != null)
                h.style.display = DisplayStyle.None;
            return;
        }
        var htext = header();
        h.style.display = DisplayStyle.Flex;
        h.text = htext ?? "";
    }
}

/// <summary>
/// A UIGroup that is a wrapper around other UIGroups. May also have nodes of its own.
/// </summary>
public abstract class CompositeUIGroup : UIGroup {
    public List<UIGroup> Groups { get; } = new();
    public CompositeUIGroup(IReadOnlyList<UIGroup> groups) : this(groups[0].Screen, groups) { }
    public CompositeUIGroup(UIRenderSpace render, IEnumerable<UIGroup> groups, IEnumerable<UINode?>? nodes = null) : base(render, nodes) {
        foreach (var g in groups)
            AddGroup(g);
    }
    public CompositeUIGroup(UIRenderSpace render, params UIGroup[] groups) : this(render,(IEnumerable<UIGroup>) groups) { }

    private void AddGroup(UIGroup g) {
        Groups.Add(g);
        g.DependentParent = this;
        EntryNodeOverride ??= g.EntryNodeOverride;
        ExitNodeOverride ??= g.ExitNodeOverride;
    }

    public void AddGroupDynamic(UIGroup g) {
        AddGroup(g);
        if (Visible)
            g.EnterShow(true);
        Controller.Redraw();
    }

    public override IEnumerable<UINode> NodesAndDependentNodes => 
        Nodes.Concat(Groups.SelectMany(g => g.NodesAndDependentNodes));

    public override UIResult Navigate(UINode node, UICommand req) => req switch {
        UICommand.Confirm => NoOp,
        UICommand.Back => GoToExitOrLeaveScreen(node, req) ?? NoOp,
        _ => NavigateAmongComposite(node, req) ?? NoOp
    };
    
    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) =>
        req switch {
            UICommand.Up or UICommand.Down or UICommand.Left or UICommand.Right => NavigateAmongComposite(current, req),
            UICommand.Back => GoToExitOrLeaveScreen(current, req),
            _ => null
        };

    public static readonly float[] _angleLimits = { 15, 35, 60 };

    /// <summary>
    /// For a position-based transition from CURRENT to NEXT, use the entry node
    ///  of the highest parent of NEXT that is a child of this composite group.
    /// </summary>
    protected UIResult? FinalizeTransition(UINode current, UINode? next) {
        if (next == null || next == current) return null;
        var g = next.Group;
        while (g != current.Group && g != null && !Groups.Contains(g))
            g = g.Parent;
        return (g != current.Group && g?.PreferredEntryNode is {} entry) ? entry : next;
    }
    
    protected virtual UIResult? NavigateAmongComposite(UINode current, UICommand dir) {
        var from = current.WorldLocation;
        if (UIFreeformGroup.FindClosest(from, dir, NodesAndDependentNodes, _angleLimits, n => n != current) 
                is {} result) 
            return FinalizeTransition(current, result);
        if (TryDelegateNavigationToEnclosure(current, dir, out var res))
            return res;
        //Reset the position to the wall opposite the direction
        // Eg. if the position is <500, 600> and the direction is right, set the position to <0, 600>
        var newFrom = M.RectFromCenter(dir switch {
            //up/down is inverted
            UICommand.Down => new Vector2(from.center.x, 0),
            UICommand.Up => new Vector2(from.center.x, current.Screen.HTML.worldBound.yMax),
            UICommand.Left => new Vector2(current.Screen.HTML.worldBound.xMax, from.center.y),
            UICommand.Right => new Vector2(0, from.center.y),
            _ => throw new Exception()
        }, new(2, 2));
        Logs.Log($"Wraparound {dir} from {from} to {newFrom}");
        //When doing wraparound, allow navigating to the same node (it should be preferred over selecting an adjacent node)
        return FinalizeTransition(current, UIFreeformGroup.FindClosest(newFrom, dir, NodesAndDependentNodes, _angleLimits));
    }
}

/// <summary>
/// A UIGroup with many subgroups oriented vertically.
/// <br/>This group has no nodes.
/// </summary>
public class VGroup : CompositeUIGroup {
    public VGroup(params UIGroup[] groups) : base(groups) { }
}

/// <summary>
/// A UIGroup with many subgroups oriented horizontally (such as several columns forming a grid).
/// <br/>This group has no nodes.
/// </summary>
public class HGroup : CompositeUIGroup {
    public HGroup(params UIGroup[] groups) : base(groups) { }
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
}

/// <summary>
/// A UIGroup with three subgroups: a left, a right, and a bottom group.
/// </summary>
public class LRBGroup : LRVGroup {
    public override bool VertIsTop => false;
    public LRBGroup(UIGroup left, UIGroup right, UIGroup vert) : base(left, right, vert) { }
}

}