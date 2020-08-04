using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmaku.DanmakuUI {

public enum NodeState {
    Focused,
    Selected,
    GroupFocused,
    Visible,
    Invisible
}

public class UIScreen {
    public UINode[] top;
    public virtual UINode First => top[0];
    [CanBeNull] public UIScreen calledBy { get; private set; }
    [CanBeNull] public UINode lastCaller { get; private set; }

    public UINode GoToNested(UINode caller, UINode target) {
        lastCaller = caller;
        target.screen.calledBy = this;
        return target;
    }

    public UINode StartingNode => lastCaller ?? top[0];

    [CanBeNull]
    public UINode TryGoBack() => calledBy?.StartingNode;

    public UIScreen(params UINode[] nodes) {
        top = nodes;
        foreach (var n in top) n.siblings = top;
        foreach (var n in ListAll()) n.screen = this;
    }

    protected UINode[] AssignNewNodes(UINode[] nodes) {
        top = nodes;
        foreach (var n in top) n.siblings = top;
        foreach (var n in ListAll()) n.screen = this;
        BuildChildren(cachedBuildMap);
        return top;
    }

    public IEnumerable<UINode> ListAll() => top.SelectMany(x => x.ListAll());

    public void ResetStates() {
        foreach (var n in ListAll()) n.state = NodeState.Invisible;
    }

    public void ApplyStates() {
        foreach (var n in ListAll()) n.ApplyState();
    }

    public void ResetNodes() {
        foreach (var n in ListAll()) n.Reset();
    }

    public VisualElement Bound { get; private set; }
    public List<ScrollView> Lists => Bound.Query<ScrollView>().ToList();
    private Dictionary<Type, VisualTreeAsset> cachedBuildMap;

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map) {
        cachedBuildMap = map;
        Bound = (overrideBuilder == null ? map[typeof(UIScreen)] : overrideBuilder).CloneTree();
        BuildChildren(map);
        return Bound;
    }
    private void BuildChildren(Dictionary<Type, VisualTreeAsset> map) {
        var lists = Lists;
        foreach (var node in ListAll()) {
            node.Build(map, lists[node.Depth]);
        }
    }

    [CanBeNull] private VisualTreeAsset overrideBuilder;

    public UIScreen With(VisualTreeAsset builder) {
        overrideBuilder = builder;
        return this;
    }
}

public class LazyUIScreen : UIScreen {
    private readonly Func<UINode[]> loader;
    public override UINode First => (top.Length > 0 ? top : AssignNewNodes(loader()))[0];

    public LazyUIScreen(Func<UINode[]> loader) : base() {
        this.loader = loader;
    }
}

public class UINode {
    private readonly UINode[] children;
    [CanBeNull] public UINode Parent { get; protected set; }
    public UINode[] siblings; //including self

    [CanBeNull] protected readonly string description;
    [CanBeNull] private readonly Func<string> descriptor;
    public UIScreen screen;
    public NodeState state = NodeState.Invisible;

    public UINode(string description, params UINode[] children) {
        this.children = children;
        this.description = description;
        foreach (var c in children) {
            c.Parent = this;
            c.siblings = children;
        }
    }
    public UINode(Func<string> descriptor, params UINode[] children) {
        this.children = children;
        this.descriptor = descriptor;
        foreach (var c in children) {
            c.Parent = this;
            c.siblings = children;
        }
    }

    public IEnumerable<UINode> ListAll() => children.SelectMany(n => n.ListAll()).Prepend(this);

    public int Depth => 1 + (Parent?.Depth ?? -1);

    public void AssignStatesFromSelected() {
        screen.ResetStates();
        foreach (var x in children) x.state = NodeState.Visible;
        foreach (var x in siblings) x.state = NodeState.GroupFocused;
        this.state = NodeState.Focused;
        for (var p = Parent; p != null; p = p.Parent) {
            foreach (var x in p.siblings) x.state = NodeState.Visible;
            p.state = NodeState.Selected;
        }
        screen.ApplyStates();
    }

    private readonly List<string> overrideClasses = new List<string>();

    public UINode With([CanBeNull] string cls) {
        if (!string.IsNullOrWhiteSpace(cls)) overrideClasses.Add(cls);
        return this;
    }

    private const string disabledClass = "disabled";

    public UINode EnabledIf(bool s) =>  With(s ? null : disabledClass);

    public void ApplyState() {
        if (descriptor != null) BindText();
        boundNode.ClearClassList();
        foreach (var c in boundClasses) {
            boundNode.AddToClassList(c);
        }
        boundNode.AddToClassList(ToClass(state));
        foreach (var cls in overrideClasses) {
            boundNode.AddToClassList(cls);
        }
    }
    
    public virtual void Reset() { }

    public virtual UINode Right() => children.Try(0) ?? this;
    public virtual UINode Left() => Parent ?? this;
    public virtual UINode Up() => siblings.ModIndex(siblings.IndexOf(this) - 1);
    public virtual UINode Down() => siblings.ModIndex(siblings.IndexOf(this) + 1);
    public virtual UINode Back() => screen.TryGoBack() ?? this;

    [CanBeNull]
    protected virtual (bool success, UINode target) _Confirm() => (false, this);

    public (bool success, UINode target) Confirm() =>
        overrideClasses.Contains(disabledClass) ? (false, this) : _Confirm();

    protected const string NodeClass = "node";

    private static string ToClass(NodeState s) {
        if (s == NodeState.Focused) return "focus";
        else if (s == NodeState.Selected) return "selected";
        else if (s == NodeState.GroupFocused) return "group";
        else if (s == NodeState.Invisible) return "invisible";
        else if (s == NodeState.Visible) return "visible";
        throw new Exception($"Couldn't resolve nodeState {s}");
    }

    public VisualElement Bound => bound;
    public VisualElement BoundN => boundNode;
    protected VisualElement bound;
    protected VisualElement boundNode;
    private ScrollView scroll;
    private string[] boundClasses;

    public void ScrollTo() => scroll.ScrollTo(bound);
    
    private void BindText() => bound.Q<Label>().text = description ?? descriptor();

    protected VisualElement BindScroll(ScrollView scroller) {
        (scroll = scroller).Add(bound);
        return bound;
    }

    public virtual VisualElement Build(Dictionary<Type, VisualTreeAsset> map, ScrollView scroller) {
        CloneTree(map);
        BindText();
        return BindScroll(scroller);
    }

    protected void CloneTree(Dictionary<Type, VisualTreeAsset> map) {
        bound = (overrideBuilder == null ? map.SearchByType(this) : overrideBuilder).CloneTree();
        boundNode = bound.Q<VisualElement>(null, NodeClass);
        boundClasses = boundNode.GetClasses().ToArray();
    }

    [CanBeNull] private VisualTreeAsset overrideBuilder;

    public UINode With(VisualTreeAsset builder) {
        overrideBuilder = builder;
        return this;
    }

    protected UINode ResolveNext(UINode next) {
        if (next == null || next == this) return next;
        return screen.GoToNested(this, next);
    }
    
    protected List<int> CacheCurrent() {
        List<int> revInds = new List<int>();
        var c = this;
        while (c != null) {
            revInds.Add(c.siblings.IndexOf(c));
            c = c.Parent ?? c.screen.calledBy?.lastCaller;
        }
        revInds.Reverse();
        return revInds;
    }
}

public class NavigateUINode : UINode {
    
    public NavigateUINode(string description, params UINode[] children) : base(description, children) {}
    protected override (bool success, UINode target) _Confirm() {
        //default going right
        var n = Right();
        return n != this ? (true, n) : (false, this);
    }
}

public class CacheNavigateUINode : UINode {
    private readonly Action<List<int>> cacher;
    public CacheNavigateUINode(Action<List<int>> cacher, string description, params UINode[] children) :
        base(description, children) {
        this.cacher = cacher;
    }
    protected override (bool success, UINode target) _Confirm() {
        cacher(CacheCurrent());
        return base._Confirm();
    }

    public override UINode Right() {
        cacher(CacheCurrent());
        return base.Right();
    }
}

public class InheritNode : UINode {
    public InheritNode(string descr) : base(descr) { }
    protected override (bool success, UINode target) _Confirm() => Parent.Confirm();
}
public class TransferNode : UINode {

    [CanBeNull] private readonly UINode target;
    [CanBeNull] private readonly UIScreen screen_target;

    public TransferNode(UINode target, string description, params UINode[] children) : base(description, children) {
        this.target = target;
    }
    public TransferNode(UIScreen target, string description, params UINode[] children) : base(description, children) {
        this.screen_target = target;
    }

    protected override (bool success, UINode target) _Confirm() {
        if (target != null) return (true, screen.GoToNested(this, target));
        return (true, screen.GoToNested(this, screen_target.First));
    }
}

public class FuncNode : UINode {

    protected readonly Func<bool> target;
    protected readonly UINode next;

    public FuncNode(Func<bool> target, string description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }
    public FuncNode(Func<bool> target, Func<string> description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }

    public FuncNode(Action target, string description, bool returnSelf = false, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, returnSelf, children) { }

    public FuncNode(Action target, string description, UINode next, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, true, children) {
        this.next = next;
    }

    protected override (bool success, UINode target) _Confirm() => (target(), ResolveNext(next));
}

public class ContinuationNode<T> : FuncNode where T : class {
    private readonly T obj;
    [CanBeNull] public Func<T, bool> continuation;

    public ContinuationNode(T target, string description, bool returnSelf = false, params UINode[] children) :
        base(null, description, returnSelf, children) {
        this.obj = target;
    }
    
    protected override (bool success, UINode target) _Confirm() {
        if (continuation == null) throw new Exception("No continuation provided");
        return (continuation(obj), ResolveNext(next));
    }
}

public class OpenUrlNode : FuncNode {

    public OpenUrlNode(string site, string description) : base(() => Application.OpenURL(site), description, true) { }
}

public class ConfirmFuncNode : FuncNode {
    private bool isConfirm;
    public ConfirmFuncNode(Action target, string description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }

    private void SetConfirm(bool newConfirm) {
        isConfirm = newConfirm;
        AssignDescription();
    }
    private void AssignDescription() {
        bound.Q<Label>().text = isConfirm ? "Are you sure?" : description;
    }
    public override void Reset() {
        SetConfirm(false);
        base.Reset();
    }
    public override UINode Back() {
        SetConfirm(false);
        return base.Back();
    }
    public override UINode Left() {
        SetConfirm(false);
        return base.Left();
    }
    public override UINode Right() {
        SetConfirm(false);
        return base.Right();
    }
    public override UINode Up() {
        SetConfirm(false);
        return base.Up();
    }
    public override UINode Down() {
        SetConfirm(false);
        return base.Down();
    }

    protected override (bool success, UINode target) _Confirm() {
        if (isConfirm) return base._Confirm();
        else {
            SetConfirm(true);
            return (true, this);
        }
    }
}

public class OptionNodeLR<T> : UINode {
    private readonly Action<T> onChange;
    private readonly (string key, T val)[] values;
    private int index;
    public T Value => values[index].val;
    
    public OptionNodeLR(string description, Action<T> onChange, (string, T)[] values, T defaulter) : base(description) {
        this.onChange = onChange;
        this.values = values;
        index = this.values.Enumerate().First(x => x.Item2.val.Equals(defaulter)).Item1;
    }

    public override UINode Left() {
        index = M.Mod(values.Length, index - 1);
        AssignValueText();
        onChange(Value);
        return this;
    }
    public override UINode Right() {
        index = M.Mod(values.Length, index + 1);
        AssignValueText();
        onChange(Value);
        return this;
    }

    private void AssignValueText() {
        bound.Q<Label>("Value").text = values[index].key;
    }
    public override VisualElement Build(Dictionary<Type, VisualTreeAsset> map, ScrollView scroller) {
        CloneTree(map);
        bound.Q<Label>("Key").text = description;
        AssignValueText();
        return BindScroll(scroller);
    }
}

}