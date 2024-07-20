using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
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

    public bool IsWeakPrefix(UIGroupHierarchy? prefix) =>
        this == prefix || IsStrictPrefix(prefix);

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

    /// <summary>
    /// Returns true if it is legal to traverse from the source to target hierarchy.
    /// <br/>The two hierarchies must share a common ancestor (and thus must be on the same screen).
    /// </summary>
    public static bool CanTraverse(UIGroupHierarchy? from, UIGroupHierarchy to) {
        if (from is null)
            return true;
        var target = GetIntersection(from, to);
        if (target is null)
            return false;
        for (var x = from; x != target; x = x.Parent) {
            if (!x.Group.NavigationCanLeaveGroup)
                return false;
        }
        return true;
    }

    /// <inheritdoc cref="CanTraverse(Danmokou.UI.XML.UIGroupHierarchy?,Danmokou.UI.XML.UIGroupHierarchy)"/>
    public static bool CanTraverse(UINode? from, UINode to) => CanTraverse(from?.Group.Hierarchy, to.Group.Hierarchy);
}

public enum GroupVisibility: int {
    TreeHidden = 0,
    TreeVisibleLocalHidden = 10,
    TreeVisible = 20,
}

public record GroupVisibilityControl(UIGroup Group) {
    public GroupVisibility ParentVisibleInTree { get; private set; } = GroupVisibility.TreeVisible;
    protected GroupVisibility LocalVisible { get; set; } = GroupVisibility.TreeVisible;
    public GroupVisibility VisibleInTree {
        get {
            if (ParentVisibleInTree is GroupVisibility.TreeHidden)
                return GroupVisibility.TreeHidden;
            return LocalVisible;
        }
    }

    public virtual Task? OnEnterGroup() => null;
    public virtual Task? OnLeaveGroup() => null;

    public virtual Task? OnReturnFromChild() => null;

    public virtual Task? OnDescendToChild(bool isEnteringPopup) => null;

    public Task ParentVisibilityUpdated(GroupVisibility? parentVisibleInTree, bool notifyRender = true) {
        var prevVisInTree = VisibleInTree;
        ParentVisibleInTree = parentVisibleInTree ?? GroupVisibility.TreeVisible;
        return UpdatedVisibility(prevVisInTree, notifyRender);
    }

    private Task UpdatedVisibility(GroupVisibility prevVisInTree, bool notifyRender = true) {
        var task0 = Task.CompletedTask;
        var visInTree = VisibleInTree;
        var changed = visInTree != prevVisInTree;
        if (!changed) return task0;
        var prevHTML = prevVisInTree.HTMLVisible();
        var nxtHTML = VisibleInTree.HTMLVisible();
        if (nxtHTML && !prevHTML) {
            if (notifyRender)
                task0 = Group.Render.UpdateVisibility();
            else Group.Render.UpdateVisibility(true);
        } else if (!nxtHTML && prevHTML) {
            if (notifyRender)
                task0 = Group.Render.UpdateVisibility();
            else Group.Render.UpdateVisibility(true);
        }
        
        if (Group.Children.Count == 0)
            return task0;
        var tasks = new Task[Group.Children.Count + 1];
        tasks[^1] = task0;
        for (int ii = 0; ii < Group.Children.Count; ++ii)
            tasks[ii] = Group.Children[ii].Visibility.ParentVisibilityUpdated(VisibleInTree, notifyRender);
        return Task.WhenAll(tasks);
    }

    public void ApplyToChildren() {
        foreach (var c in Group.Children)
            c.Visibility.ParentVisibilityUpdated(VisibleInTree, false);
    }

    public override string ToString() => $"{this.GetType().RName()}({LocalVisible}, {ParentVisibleInTree})";

    public record UpdateOnLeaveHide : GroupVisibilityControl {
        private bool useLocalHiding;
        public UpdateOnLeaveHide(UIGroup Group, bool useLocalHiding = false) : base(Group) {
            LocalVisible = GroupVisibility.TreeHidden;
            this.useLocalHiding = useLocalHiding;
        }

        public override Task? OnEnterGroup() {
            var prevVisInTree = VisibleInTree;
            LocalVisible = GroupVisibility.TreeVisible;
            return UpdatedVisibility(prevVisInTree);
        }
        public override Task? OnLeaveGroup() {
            var prevVisInTree = VisibleInTree;
            LocalVisible = GroupVisibility.TreeHidden;
            return UpdatedVisibility(prevVisInTree);
        }

        public override Task? OnReturnFromChild() {
            if (!useLocalHiding) return null;
            var prevVisInTree = VisibleInTree;
            LocalVisible = GroupVisibility.TreeVisible;
            return UpdatedVisibility(prevVisInTree);
        }

        public override Task? OnDescendToChild(bool isEnteringPopup) {
            if (!useLocalHiding || isEnteringPopup) return null;
            var prevVisInTree = VisibleInTree;
            LocalVisible = GroupVisibility.TreeVisibleLocalHidden;
            return UpdatedVisibility(prevVisInTree);
        }

        public override string ToString() => base.ToString();
    }
}

public abstract class UIGroup {
    public static readonly UIResult NoOp = new StayOnNode(true);
    public static readonly UIResult SilentNoOp = new StayOnNode(StayOnNodeType.Silent);
    private GroupVisibilityControl _visibility = null!;
    public GroupVisibilityControl Visibility {
        get => _visibility;
        set {
            var prev = _visibility;
            _visibility = value;
            value.ApplyToChildren();
            if (prev != null!)
                Render.UpdateVisibility(true);
        }
    }
    public bool Visible => Visibility.VisibleInTree.HTMLVisible();

    /// <summary>
    /// Set the group's visibility to be enabled only while navigation is in this group or any child of this group.
    /// (By default, groups are always visible.)
    /// </summary>
    public UIGroup WithLeaveHideVisibility() {
        Visibility = new GroupVisibilityControl.UpdateOnLeaveHide(this);
        return this;
    }
    
    /// <summary>
    /// Set the group's visibility to be enabled only while navigation is in this group,
    /// <br/>and to be locally hidden when navigation is in a child of this group.
    /// (By default, groups are always visible.)
    /// </summary>
    public UIGroup WithLocalLeaveHideVisibility() {
        Visibility = new GroupVisibilityControl.UpdateOnLeaveHide(this, useLocalHiding:true);
        return this;
    }

    /// <summary>
    /// Whether or not user focus can leave this group via mouse/keyboard control. (True by default, except for popups.)
    /// </summary>
    public bool NavigationCanLeaveGroup { get; set; } = true;
    public UIScreen Screen { get; }
    public UIRenderSpace Render { get; }

    /// <summary>
    /// Called after all nodes in this group are built.
    /// </summary>
    public Action<UIGroup>? OnBuilt { get; private set; }

    public UIGroup WithOnBuilt(Action<UIGroup>? act) {
        if (buildMap != null)
            act?.Invoke(this);
        else if (act != null)
            OnBuilt = OnBuilt.Then(act);
        return this;
    }

    private UIGroup? _parent;
    /// <summary>
    /// The UI group that contains this UI group, and to which navigation delegates if internal navigation fails.
    /// </summary>
    public UIGroup? Parent {
        get => _parent;
        set {
            _parent?.Children.Remove(this);
            _parent = value;
            _parent?.Children.Add(this);
            _ = Visibility.ParentVisibilityUpdated(_parent?.Visibility.VisibleInTree, false);
        }
    }

    /// <summary>
    /// The set of UI groups contained by this group.
    /// Encompasses component groups for <see cref="CompositeUIGroup"/>,
    /// as well as show/hide groups and popups.
    /// </summary>
    public List<UIGroup> Children { get; } = new();

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

    public UIGroup WithNodeMod(Action<UINode> mod) {
        foreach (var n in Nodes)
            mod(n);
        return this;
    }
    private bool _interactable = true;
    public bool Interactable {
        get => _interactable;
        set {
            _interactable = value;
            if (!value && nodes != null)
                foreach (var n in Nodes)
                    n.ReprocessSelection();
        }
    }

    /// <summary>
    /// Node to go to when trying to enter this group.
    /// <br/>Note that this node may be in a descendant of this group.
    /// </summary>
    public Delayed<UINode?>? EntryNodeOverride { get; set; }
    public UINode? EntryNodeBottomOverride { get; set; }
    public Func<int>? EntryIndexOverride { get; init; }
    /// <summary>
    /// Node to go to when pressing the back key from within this group. Usually something like "return to menu".
    /// <br/>Note that this node may be in a descendant of this group.
    /// </summary>
    public UINode? ExitNodeOverride { get; set; }
    public int? ExitIndexOverride { get; init; }

    /// <summary>
    /// Run code when navigation moves to this group or a descendant of this group.
    /// </summary>
    public Func<UIGroup, Task?>? OnEnter { private get; set; }

    public UIGroup WithOnEnter(Action x) {
        OnEnter = _ => { x(); return null; };
        return this;
    }
    
    /// <summary>
    /// Run code when navigation moves outside of this group and all descendants of this group.
    /// </summary>
    public Func<UIGroup, Task?>? OnLeave { private get; set; }

    public UIGroup WithOnLeave(Action x) {
        OnLeave = _ => { x(); return null; };
        return this;
    }

    /// <summary>
    /// Run code when navigation moves from a descendant of this group to this group.
    /// </summary>
    public Func<UIGroup, Task?>? OnReturnFromChild { private get; set; }

    /// <summary>
    /// Run code when navigation moves from this group to a descendant of this group.
    /// <br/>bool: True iff the child group is a popup.
    /// </summary>
    public Func<UIGroup, bool, Task?>? OnGoToChild { private get; set; }
    public UIGroup OnEnterOrReturnFromChild(Action<UIGroup> cb) {
        OnEnter = OnReturnFromChild = grp => {
            cb(grp);
            return null;
        };
        return this;
    }

    public bool DestroyOnLeave { get; set; } = false;


    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIGroupHierarchy Hierarchy => new(this, Parent?.Hierarchy);
    public UIController Controller => Screen.Controller;
    public bool IsCurrent => Controller.Current != null && Nodes.Contains(Controller.Current);
    public UINode? PreferredEntryNode {
        get {
            if (EntryNodeOverride is { } eno)
                return eno.Value;
            if (EntryIndexOverride != null)
                return Nodes.ModIndex(EntryIndexOverride());
            return null;
        }
    }
    public UINode? FirstInteractableNode {
        get {
            foreach (var n in Nodes)
                if (n.AllowInteraction)
                    return n;
            return null;
        }
    }
    public virtual UINode? MaybeEntryNode => PreferredEntryNode ?? FirstInteractableNode;
    
    /// <summary>
    /// The node that should receive focus when entering this group.
    /// May be in a descendant of this group.
    /// </summary>
    public UINode EntryNode => MaybeEntryNode ?? throw new Exception($"UIGroup {this} has no entry node");
    public UINode EntryNodeFromBottom {
        get {
            if (EntryNodeBottomOverride != null)
                return EntryNodeBottomOverride;
            return EntryNode;
        }
    }
    public UINode? ExitNode => ExitNodeOverride ?? (ExitIndexOverride.Try(out var i) ? Nodes.ModIndex(i) : null);
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

    /// <summary>
    /// Set `group.Parent` on all the provided child groups.
    /// </summary>
    public UIGroup WithChildren(params UIGroup[] children) {
        foreach (var cgroup in children)
            cgroup.Parent = this;
        return this;
    }

    /// <summary>
    /// Set this UIGroup as the first in the screen (calls <see cref="UIScreen"/>.<see cref="UIScreen.SetFirst"/>).
    /// </summary>
    public UIGroup SetFirst() {
        Screen.SetFirst(this);
        return this;
    }

public UIGroup(UIScreen container, UIRenderSpace? render, IEnumerable<UINode?>? nodes) {
        Screen = container;
        Render = render ?? container.ColumnRender(0);
        Visibility = new GroupVisibilityControl(this);
        this.nodes = nodes?.FilterNone().ToList() ?? new();
        foreach (var n in this.nodes)
            n.Group = this;
        _ = Render.AddSource(this).ContinueWithSync();
        Screen.AddGroup(this);
    }

    public UIGroup(UIRenderSpace render, IEnumerable<UINode?>? nodes) : this(render.Screen, render, nodes) { }

    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        foreach (var n in nodes)
            n.Build(map);
        //Ensure that renderer HTML is created even if this group is empty
        // (occurs occasionally with UIFreeformGroup)
        _ = Render.HTML;
    }

    public void AddNodeDynamic(UINode n) {
        Nodes.Add(n);
        n.Group = this;
        if (buildMap != null)
            n.Build(buildMap);
        Controller.Redraw();
    }

    public void AddNodeDynamic(IUIView view) => AddNodeDynamic(new UINode(view));

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
    /// If permitted by <see cref="NavigationCanLeaveGroup"/>, delegate navigation to the parent group by calling <see cref="Parent"/>.<see cref="NavigateOutOfEnclosed"/>.
    /// </summary>
    protected bool TryDelegateNavigationToEnclosure(UINode current, UICommand req, out UIResult res) {
        res = default!;
        return NavigationCanLeaveGroup &&
               Parent != null && Parent.NavigateOutOfEnclosed(this, current, req).Try(out res);
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
            if (Nodes[ii].AllowKBInteraction)
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
            if (Nodes[ii].AllowKBInteraction)
                break;
        }
        return new GoToNode(this, ii);
    }

    protected UIResult? GoToShowHideGroupIfExists(UINode node, UICommand dir) {
        if (node.ShowHideGroup == null || !node.IsEnabled) return null;
        if (node.ShowHideGroup.MaybeEntryNode is {} en) return en;
        if (UIFreeformGroup.FindClosest(node.WorldLocation, dir, node.ShowHideGroup.NodesAndDependentNodes,
                   CompositeUIGroup._angleLimits, x => x != node) is { } n)
            return n;
        return null;
    }

    protected UIResult? GoToShowHideGroupFromBelowIfExists(UINode node, UICommand dir) {
        if (node.ShowHideGroup == null || !node.IsEnabled) return null;
        if (node.ShowHideGroup.MaybeEntryNode is {}) return node.ShowHideGroup.EntryNodeFromBottom;
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

    /// <summary>
    /// Called when navigation moves from a *parent* of this group to this group.
    /// (Moving from a child does not trigger this.)
    /// </summary>
    public Task? EnterGroup() {
        //don't await visibility animations (eg. popup scale-in) during enter group;
        // this allows the user to navigate the new group while the animation is still playing,
        // which is marginally more responsive.
        _ = Visibility.OnEnterGroup()?.ContinueWithSync();
        return OnEnter?.Invoke(this);
    }

    /// <summary>
    /// Called when navigation returns from this group to a *parent* of this group.
    /// (Moving to a child does not trigger this.)
    /// </summary>
    public Task LeaveGroup() {
        var task = Visibility.OnLeaveGroup().And(OnLeave?.Invoke(this));
        if (DestroyOnLeave)
            return task.ContinueWithSync(Destroy);
        return task;
    }

    /// <summary>
    /// Called when navigation returns from a child of this group, or a different screen, to this group.
    /// </summary>
    public Task? ReturnFromChild() {
        return Visibility.OnReturnFromChild().And(OnReturnFromChild?.Invoke(this));
    }

    /// <summary>
    /// Called when navigation goes from this group to a child of this group or a different screen.
    /// </summary>
    public Task? DescendToChild(bool isEnteringPopup) {
        return Visibility.OnDescendToChild(isEnteringPopup).And(OnGoToChild?.Invoke(this, isEnteringPopup));
    }

    /// <summary>
    /// Remove all nodes in this group. (The group is still valid and can still be used.)
    /// </summary>
    public void ClearNodes() {
        foreach (var n in Nodes.ToList())
            n.Remove();
        Nodes.Clear();
    }

    /// <summary>
    /// Remove all nodes in this group, then remove the group from the screen and render config.
    /// </summary>
    public void Destroy() {
        Screen.Groups.Remove(this);
        if (Render is UIRenderConstructed uirc && Render.AllSourcesDescendFrom(this)) {
            uirc.Destroy();
        } else {
            _ = Render.RemoveSource(this).ContinueWithSync();
        }
        ClearNodes();
        Parent = null;
        if (this is CompositeUIGroup cuig)
            foreach (var g in cuig.Components.ToList())
                g.Destroy();
    }

    /// <summary>
    /// Mark all nodes as destroyed. Does not affect HTML.
    /// <br/>If this is a lazy-loaded group that has not yet loaded, then this function is a no-op.
    /// <br/>Call this when the menu containing this group is being destroyed.
    /// </summary>
    public void MarkNodesDestroyed() {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (nodes is null) return;
        foreach (var n in nodes)
            n.MarkDestroyed();
    }
    
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

    public bool AllowDefaultCtxMenu { get; set; } = false;
    public bool EasyExit { get; set; }
    public float? OverlayAlphaOverride { get; set; }

    /// <summary>
    /// Create a popup with a row of action buttons at the bottom.
    /// </summary>
    /// <param name="source">The node that spawned the popup</param>
    /// <param name="header">Popup header (optional)</param>
    /// <param name="bodyInner">Constructor for the UIGroup containing the popup messages, entry box, etc</param>
    /// <param name="buttons">Configuration for action buttons</param>
    /// <param name="prefab">Prefab to use for the popup</param>
    /// <param name="builder">Extra on-build configuration for the popup HTML</param>
    /// <returns></returns>
    public static PopupUIGroup CreatePopup(UINode source, LString? header, Func<UIRenderSpace, UIGroup> bodyInner,
        PopupButtonOpts buttons, VisualTreeAsset? prefab = null, Action<UIRenderConstructed, VisualElement>? builder = null) {
        var render = MakeRenderer(source.Screen.AbsoluteTerritory, prefab != null ? prefab : XMLUtils.Prefabs.Popup, builder);
        var bodyGroup = bodyInner(new UIRenderExplicit(render, html => html.Q("BodyHTML")));
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
            new UIRow(new UIRenderExplicit(render, html => html.Q("OptionsHTML")), opts) {
                EntryNodeOverride = entry,
                ExitNodeOverride = exit
            })) {
            EasyExit = opts.Any(o => o is UIButton { Type: UIButton.ButtonType.Cancel}),
            EntryNodeOverride = bodyGroup.MaybeEntryNode ?? entry,
            ExitNodeOverride = exit
        };
        return p;
    }

    public static PopupUIGroup CreateContextMenu(UINode source, params UINode?[] options) {
        var render = MakeRenderer(source.Screen.AbsoluteTerritory, XMLUtils.Prefabs.ContextMenu);
        var ctxMenuPos = source.HTML.worldBound.max-Vector2.Min(new(120, 120), source.HTML.worldBound.size * 0.2f);
        if (source.Controller.LastPointerLocation is { } loc && source.HTML.worldBound.Contains(loc))
            ctxMenuPos = loc;
        render.HTML.ConfigureAbsolute(XMLUtils.Pivot.TopLeft).WithAbsolutePosition(ctxMenuPos);
        var back = new FuncNode(LocalizedStrings.Generic.generic_back, UIButton.GoBackTwiceCommand<FuncNode>(source));
        var close = new FuncNode(LocalizedStrings.Generic.generic_close, UIButton.GoBackCommand<FuncNode>(source));
        //NB: you can press X *once* to leave an options menu.
        // If you add a ExitNodeOverride to the UIColumn, then you'll need to press it twice (as with standard popups)
        var grp = new UIColumn(new UIRenderConstructed(render, new(XMLUtils.AddColumn)), 
            options.Prepend(back).Append(close).Select(x => x?.WithCSS(XMLUtils.noPointerClass)));
        var p = new PopupUIGroup(render, null, source, grp) {
            EntryNodeOverride = back,
            ExitNodeOverride = close,
            EasyExit = true,
            OverlayAlphaOverride = 0,
        };
        return p;
    }

    public static PopupUIGroup CreateDropdown(UINode src, Selector selector) {
        var target = src.HTML.Q(null, XMLUtils.dropdownTarget) ?? src.BodyOrNodeHTML;
        var render = MakeRenderer(src.Screen.AbsoluteTerritory, XMLUtils.Prefabs.Dropdown, 
            (_, ve) => {
                ve.style.width = target.worldBound.width;
                ve.AddScrollColumn().style.maxHeight = 500;
            });
        target.SetTooltipAbsolutePosition(render.HTML.ConfigureAbsolute(XMLUtils.Pivot.Top));
        var grp = new UIColumn(new UIRenderColumn(render, 0), selector.MakeNodes(src));
        return new PopupUIGroup(render, null, src, grp) {
            EntryNodeOverride = grp.EntryNode,
            EasyExit = true,
            AllowDefaultCtxMenu = true,
            OverlayAlphaOverride = 0,
        };
    }

    private static UIRenderSpace MakeRenderer(UIRenderAbsoluteTerritory at, VisualTreeAsset prefab, Action<UIRenderConstructed, VisualElement>? builder = null) {
        var render = new UIRenderConstructed(at, prefab, builder) {
            AnimateOutWithParent = true
        }.WithPopupAnim();
        //Don't allow pointer events to hit the underlying Absolute Territory
        render.HTML.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
        return render;
    }
    
    public PopupUIGroup(UIRenderSpace r, LString? header, UINode source, UIGroup body) :
        base(r, body) {
        this.Visibility = new GroupVisibilityControl.UpdateOnLeaveHide(this);
        this.Source = source;
        this.Parent = source.Group;
        this.NavigationCanLeaveGroup = false;
        this.DestroyOnLeave = true;
        WithOnBuilt(_ => {
            var h = Render.HTML.Q<Label>("Header");
            if (header is null) {
                if (h != null)
                    h.style.display = DisplayStyle.None;
                return;
            }
            h.style.display = DisplayStyle.Flex;
            h.text = header;
        });
    }

    public override UIResult? NavigateOutOfEnclosed(UIGroup enclosed, UINode current, UICommand req) => req switch {
        UICommand.Back => EasyExit ? Source.ReturnToGroup : NoOp,
        _ => null
    };
}

/// <summary>
/// A UIGroup that is a wrapper around other UIGroups. May also have nodes of its own.
/// </summary>
public abstract class CompositeUIGroup : UIGroup {
    public List<UIGroup> Components { get; } = new();

    public override UINode? MaybeEntryNode {
        get {
            if (base.MaybeEntryNode is { } pen)
                return pen;
            foreach (var c in Components) {
                if (c.MaybeEntryNode is { } en)
                    return en;
            }
            return null;
        }
    }

    public CompositeUIGroup(IReadOnlyList<UIGroup> groups) : this(groups[0].Screen, groups) { }
    public CompositeUIGroup(UIRenderSpace render, IEnumerable<UIGroup> groups, IEnumerable<UINode?>? nodes = null) : base(render, nodes) {
        foreach (var g in groups)
            AddGroup(g);
    }
    public CompositeUIGroup(UIRenderSpace render, params UIGroup[] groups) : this(render,(IEnumerable<UIGroup>) groups) { }

    private void AddGroup(UIGroup g) {
        Components.Add(g);
        g.Parent = this;
    }

    public void AddGroupDynamic(UIGroup g) {
        AddGroup(g);
        Controller.Redraw();
    }

    public override IEnumerable<UINode> NodesAndDependentNodes => 
        Nodes.Concat(Components.SelectMany(g => g.NodesAndDependentNodes));

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
        while (g != current.Group && g != null && !Components.Contains(g))
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
        //Logs.Log($"Wraparound {dir} from {from} to {newFrom}");
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