using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Core;
using DMK.DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace DMK.UI.XML {
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
    public UIScreen? calledBy { get; private set; }
    public UINode? lastCaller { get; private set; }

    public UINode GoToNested(UINode caller, UINode target) {
        lastCaller = caller;
        target.screen.calledBy = this;
        target.screen.onEnter?.Invoke();
        onExit?.Invoke();
        return target;
    }

    public UINode StartingNode => lastCaller ?? top[0];
    public UINode? ExitNode { get; set; }

    public UINode? GoBack() {
        if (calledBy?.StartingNode != null) {
            onExit?.Invoke();
            calledBy.onEnter?.Invoke();
        }
        return calledBy?.StartingNode;
    }

    public void RunPreExit() => onPreExit?.Invoke();
    public void RunPreEnter() => onPreEnter?.Invoke();
    public void RunPostEnter() => onPostEnter?.Invoke();

    public UIScreen(params UINode?[] nodes) {
        top = nodes.Where(x => x != null).ToArray()!;
        foreach (var n in top) n.Siblings = top;
        foreach (var n in ListAll()) n.screen = this;
    }

    protected UINode[] AssignNewNodes(UINode[] nodes) {
        top = nodes;
        foreach (var n in top) n.Siblings = top;
        foreach (var n in ListAll()) n.screen = this;
        BuildChildren();
        return top;
    }

    public IEnumerable<UINode> ListAll() => top.SelectMany(x => x.ListAll());
    public bool HasNode(UINode x) => ListAll().Contains(x);

    public void ResetStates() {
        foreach (var n in ListAll()) n.state = NodeState.Invisible;
    }

    public void ApplyStates() {
        foreach (var n in ListAll()) n.ApplyState();
    }

    public void ResetNodeProgress() {
        foreach (var n in ListAll()) n.ResetProgress();
    }

    public VisualElement Bound { get; private set; } = null!;

    private List<ScrollView>? _lists;
    public List<ScrollView> Lists => _lists ??= Bound.Query<ScrollView>().ToList();
    private Dictionary<Type, VisualTreeAsset> buildMap = null!;

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        Bound = (overrideBuilder == null ? map[typeof(UIScreen)] : overrideBuilder).CloneTree();
        BuildChildren();
        return Bound;
    }
    private void BuildChildren() => ListAll().ForEach(BuildChild);
    public void BuildChild(UINode node) => node.Build(buildMap, Lists[node.Depth]);

    private VisualTreeAsset? overrideBuilder;

    public UIScreen With(VisualTreeAsset builder) {
        overrideBuilder = builder;
        return this;
    }
    
    private Action? onPreExit;
    /// <summary>
    /// This is run on exit transition start
    /// </summary>
    public UIScreen OnPreExit(Action cb) {
        onPreExit = cb;
        return this;
    }
    private Action? onExit;
    /// <summary>
    /// This is run at exit transition midpoint
    /// </summary>
    public UIScreen OnExit(Action cb) {
        onExit = cb;
        return this;
    }

    private Action? onEnter;
    /// <summary>
    /// This is run at entry transition midpoint
    /// </summary>
    public UIScreen OnEnter(Action cb) {
        onEnter = cb;
        return this;
    }
    private Action? onPreEnter;
    /// <summary>
    /// This is run on entry transition start
    /// </summary>
    public UIScreen OnPreEnter(Action cb) {
        onPreEnter = cb;
        return this;
    }
    private Action? onPostEnter;
    /// <summary>
    /// This is run on entry transition end
    /// </summary>
    public UIScreen OnPostEnter(Action cb) {
        onPostEnter = cb;
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
    public readonly UINode[] children;
    public UINode? Parent { get; private set; }
    public UINode[] Siblings { get; set; } = null!; //including self
    private UINode[]? _sameDepthSiblings;
    public UINode[] SameDepthSiblings =>
        _sameDepthSiblings ??= Siblings.Where(s => s.Depth == Depth).ToArray();


    public LocalizedString Description => descriptor();
    //sorry but there's a use case where i need to modify this in the initializer. see TextInputNode
    protected Func<LocalizedString> descriptor;
    public UIScreen screen = null!;
    public NodeState state = NodeState.Invisible;

    public UINode(LocalizedString description, params UINode[] children) : this(() => description, children) { }
    public UINode(Func<LocalizedString> descriptor, params UINode[] children) {
        this.children = children;
        this.descriptor = descriptor;
        foreach (var c in children) {
            c.Parent = this;
            c.Siblings = children;
        }
    }

    public IEnumerable<UINode> ListAll() => children.SelectMany(n => n.ListAll()).Prepend(this);

    public int Depth => Parent?.ChildDepth ?? 0;
    protected virtual int ChildDepth => 1 + Depth;

    protected static void AssignParentingStates(UINode? p) {
        for (; p != null; p = p.Parent) {
            foreach (var x in p.SameDepthSiblings) x.state = NodeState.Visible;
            p.state = NodeState.Selected;
        }
    }
    protected virtual void AssignParentStatesFromSelected() => AssignParentingStates(Parent);
    public virtual void AssignStatesFromSelected() {
        screen.ResetStates();
        foreach (var x in children) x.state = NodeState.Visible;
        foreach (var x in SameDepthSiblings) x.state = NodeState.GroupFocused;
        this.state = NodeState.Focused;
        AssignParentStatesFromSelected();
        screen.ApplyStates();
    }

    private readonly List<string> overrideClasses = new List<string>();
    private readonly List<Action<VisualElement>> overrideInline = new List<Action<VisualElement>>();

    public UINode With(params string?[] clss) {
        foreach (var cls in clss) {
            if (!string.IsNullOrWhiteSpace(cls)) overrideClasses.Add(cls!);
        }
        return this;
    }

    public UINode With(params Action<VisualElement>?[] inline) {
        foreach (var func in inline) {
            if (func != null) overrideInline.Add(func);
        }
        return this;
    }

    private const string disabledClass = "disabled";

    private Func<bool>? enableCheck;
    public UINode EnabledIf(Func<bool> s) {
        enableCheck = s;
        return this;
    }
    public UINode EnabledIf(bool s) => EnabledIf(() => s);

    public void ApplyState() {
        BindText();
        boundNode.ClearClassList();
        foreach (var c in boundClasses) {
            boundNode.AddToClassList(c);
        }
        boundNode.AddToClassList(ToClass(state));
        foreach (var cls in overrideClasses) {
            boundNode.AddToClassList(cls);
        }
        foreach (var inline in overrideInline) {
            inline(boundNode);
        }
        confirmEnabled = (enableCheck?.Invoke() ?? true);
        if (!confirmEnabled) boundNode.AddToClassList(disabledClass);
    }

    private bool confirmEnabled = true; //otherwise doesn't work with ReturnTo
    
    public virtual void ResetProgress() { }

    private Func<UINode>? _overrideRight;
    public UINode SetRightOverride(Func<UINode> overr) {
        _overrideRight = overr;
        return this;
    }

    private int rightChildIndex = 0;

    public UINode SetRightChildIndex(int index) {
        rightChildIndex = index;
        return this;
    }
    public virtual UINode Right() => confirmEnabled ? 
        (_overrideRight?.Invoke() ?? children.Try(M.Mod(Math.Max(1, children.Length), rightChildIndex)) ?? this) : this;
    
    private Func<UINode>? _overrideLeft;
    public UINode SetLeftOverride(Func<UINode> overr) {
        _overrideLeft = overr;
        return this;
    }

    public virtual UINode? CustomEventHandling() => null;
    public virtual UINode Left() => _overrideLeft?.Invoke() ?? Parent ?? this;
    
    private Func<UINode>? _overrideUp;
    public UINode SetUpOverride(Func<UINode> overr) {
        _overrideUp = overr;
        return this;
    }
    public virtual UINode Up() => _overrideUp?.Invoke() ??
                                  SameDepthSiblings.ModIndex(SameDepthSiblings.IndexOf(this) - 1);
    
    private Func<UINode>? _overrideDown;
    public UINode SetDownOverride(Func<UINode> overr) {
        _overrideDown = overr;
        return this;
    }
    public virtual UINode Down() => _overrideDown?.Invoke() ?? 
                                    SameDepthSiblings.ModIndex(SameDepthSiblings.IndexOf(this) + 1);

    private Func<UINode>? _overrideBack;
    public UINode SetBackOverride(Func<UINode> overr) {
        _overrideBack = overr;
        return this;
    }

    public virtual UINode Back() => _overrideBack?.Invoke() ?? screen.calledBy?.StartingNode ?? screen.ExitNode ?? this;

    private Func<bool>? _passthrough;
    public bool Passthrough => _passthrough?.Invoke() ?? false;

    public UINode PassthroughIf(Func<bool> passthrough) {
        _passthrough = passthrough;
        return this;
    }

    private Action<UINode>? _onVisit = null;

    private Action<UINode?>? _onLeave = null;

    public UINode SetOnVisit(Action<UINode> onVisit) {
        _onVisit = onVisit;
        return this;
    }
    public void OnVisit(UINode prev) {
        _onVisit?.Invoke(prev);
    }
    public UINode SetOnLeave(Action<UINode?> onLeave) {
        _onLeave = onLeave;
        return this;
    }
    public void OnLeave(UINode? prev) {
        _onLeave?.Invoke(prev);
    }

    protected virtual (bool success, UINode? target) _Confirm() => _overrideConfirm?.Invoke() ?? (false, this);

    private Func<(bool, UINode?)>? _overrideConfirm;
    public UINode SetConfirmOverride(Func<(bool, UINode?)> overr) {
        _overrideConfirm = overr;
        return this;
    }

    public (bool success, UINode? target) Confirm() {
        if (confirmEnabled) {
            var (success, target) = _Confirm();
            if (!success || target == null || screen.HasNode(target)) return (success, target);
            else return (true, screen.GoToNested(this, target));
        } else return (false, this);
    }

    public (bool success, UINode? target) Confirm_DontNest() =>
        confirmEnabled ? _Confirm() : (false, this);

    protected const string NodeClass = "node";

    private Func<bool?>? _visible;

    public UINode VisibleIf(Func<bool?> visible) {
        _visible = visible;
        _passthrough ??= (() => _visible?.Invoke() == false);
        return this;
    }
    private string ToClass(NodeState s) {
        var visOverride = _visible?.Invoke();
        if (visOverride == false) return "invisible";
        if (s == NodeState.Focused) return "focus";
        else if (s == NodeState.Selected) return "selected";
        else if (s == NodeState.GroupFocused || visOverride == true) return "group";
        else if (s == NodeState.Invisible) return "invisible";
        else if (s == NodeState.Visible) return "visible";
        throw new Exception($"Couldn't resolve nodeState {s}");
    }

    public VisualElement Bound => bound;
    public VisualElement BoundN => boundNode;
    protected VisualElement bound = null!;
    protected VisualElement boundNode = null!;
    private ScrollView scroll = null!;
    private string[] boundClasses = new string[0];

    public void ScrollTo() => scroll.ScrollTo(bound);
    
    protected virtual void BindText() => bound.Q<Label>().text = Description;

    protected VisualElement BindScroll(ScrollView scroller) {
        (scroll = scroller).Add(bound);
        return bound;
    }

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map, ScrollView scroller) {
        CloneTree(map);
        BindText();
        return BindScroll(scroller);
    }

    protected void CloneTree(Dictionary<Type, VisualTreeAsset> map) {
        bound = (overrideBuilder == null ? map.SearchByType(this, true) : overrideBuilder).CloneTree();
        boundNode = bound.Q<VisualElement>(null!, NodeClass);
        boundClasses = boundNode.GetClasses().ToArray();
    }

    private VisualTreeAsset? overrideBuilder;

    public UINode With(VisualTreeAsset builder) {
        overrideBuilder = builder;
        return this;
    }
    
    protected List<XMLMenu.CacheInstruction> CacheCurrent() {
        var revInds = new List<XMLMenu.CacheInstruction>();
        UINode? c = this;
        while (c != null) {
            if (c is IOptionNodeLR opt) {
                revInds.Add(XMLMenu.CacheInstruction.ToOption(opt.Index));
            }
            if (c.Parent != null) {
                revInds.Add(XMLMenu.CacheInstruction.ToChild(c.Siblings.IndexOf(c)));
                c = c.Parent;
            } else if (c.screen.calledBy?.lastCaller != null) {
                revInds.Add(XMLMenu.CacheInstruction.ToSibling(c.Siblings.IndexOf(c)));
                revInds.Add(XMLMenu.CacheInstruction.Confirm);
                c = c.screen.calledBy?.lastCaller;
            } else {
                revInds.Add(XMLMenu.CacheInstruction.ToSibling(c.Siblings.IndexOf(c)));
                c = null;
            }
        }
        revInds.Reverse();
        return revInds;
    }
}

public class TwoLabelUINode : UINode {
    private readonly Func<LocalizedString> desc2;
    public TwoLabelUINode(Func<LocalizedString> desc1, Func<LocalizedString> desc2, params UINode[] children) :
        base(desc1, children) {
        this.desc2 = desc2;

    }
    public TwoLabelUINode(LocalizedString desc1, Func<LocalizedString> desc2, params UINode[] children) :
        base(desc1, children) {
        this.desc2 = desc2;

    }

    protected override void BindText() {
        bound.Q<Label>("Label").text = Description;
        bound.Q<Label>("Label2").text = desc2();
        base.BindText();
    }
}

public class NavigateUINode : UINode {
    
    public NavigateUINode(LocalizedString description, params UINode[] children) : base(description, children) {}
    public NavigateUINode(Func<LocalizedString> description, params UINode[] children) : base(description, children) {}
    protected override (bool success, UINode? target) _Confirm() {
        //default going right
        var n = Right();
        return n != this ? (true, n) : base._Confirm();
    }
}

public class CacheNavigateUINode : NavigateUINode {
    private readonly Action<List<XMLMenu.CacheInstruction>> cacher;
    public CacheNavigateUINode(Action<List<XMLMenu.CacheInstruction>> cacher, LocalizedString description, params UINode[] children) :
        base(description, children) {
        this.cacher = cacher;
    }
    public CacheNavigateUINode(Action<List<XMLMenu.CacheInstruction>> cacher, Func<LocalizedString> description, params UINode[] children) :
        base(description, children) {
        this.cacher = cacher;
    }
    protected override (bool success, UINode? target) _Confirm() {
        cacher(CacheCurrent());
        return base._Confirm();
    }

    public override UINode Right() {
        cacher(CacheCurrent());
        return base.Right();
    }
}

public class TransferNode : UINode {

    private readonly UIScreen screen_target;

    public TransferNode(UIScreen target, LocalizedString description, params UINode[] children) : base(description, children) {
        this.screen_target = target;
    }

    protected override (bool success, UINode target) _Confirm() {
        return (true, screen_target.First);
    }
}

public class FuncNode : UINode {

    protected readonly Func<bool> target;
    protected readonly UINode? next;

    public FuncNode(Func<bool> target, LocalizedString description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }
    public FuncNode(Func<bool> target, Func<LocalizedString> description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }

    public FuncNode(Action target, LocalizedString description, bool returnSelf = false, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, returnSelf, children) { }
    public FuncNode(Action target, Func<LocalizedString> description, bool returnSelf = false, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, returnSelf, children) { }

    public FuncNode(Action target, LocalizedString description, UINode next, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, true, children) {
        this.next = next;
    }

    protected override (bool success, UINode? target) _Confirm() => (target(), next);
}

public class OpenUrlNode : FuncNode {

    public OpenUrlNode(string site, LocalizedString description) : base(() => Application.OpenURL(site), description, true) { }
}

public class ConfirmFuncNode : FuncNode {
    private bool isConfirm;
    public ConfirmFuncNode(Action target, LocalizedString description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }
    public ConfirmFuncNode(Action target, Func<LocalizedString> description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }
    public ConfirmFuncNode(Func<bool> target, LocalizedString description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }

    private void SetConfirm(bool newConfirm) {
        isConfirm = newConfirm;
    }
    protected override void BindText() {
        bound.Q<Label>().text = isConfirm ? LocalizedStrings.UI.are_you_sure : Description;
    }
    public override void ResetProgress() {
        SetConfirm(false);
        base.ResetProgress();
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

    protected override (bool success, UINode? target) _Confirm() {
        if (isConfirm) {
            SetConfirm(false);
            return base._Confirm();
        } else {
            SetConfirm(true);
            return (true, this);
        }
    }
}

public class PassthroughNode : UINode {
    public PassthroughNode(LocalizedString description) : base(description) {
        PassthroughIf(() => true);
    }
    public PassthroughNode(Func<LocalizedString> description) : base(description) { 
        PassthroughIf(() => true);
    }
}

public interface IOptionNodeLR {
    int Index { get; set; }
}
public interface IComplexOptionNodeLR {
    int Index { get; set; }
}
/// <summary>
/// Contains complex children that have their own VisualAsset representation, rather than just text.
/// </summary>
public class DynamicComplexOptionNodeLR<T> : UINode, IComplexOptionNodeLR {
    private readonly Action<T> onChange;
    private readonly Func<T[]> values;
    private readonly Action<T, VisualElement, bool> binder;
    private readonly VisualTreeAsset objectTree;
    public int Index { get; set; }
    private VisualElement[] boundChildren = new VisualElement[0];
    
    public DynamicComplexOptionNodeLR(LocalizedString description, VisualTreeAsset objectTree, Action<T> onChange, Func<T[]> values, Action<T, VisualElement, bool> binder) : base(description) {
        this.onChange = onChange;
        this.values = values;
        this.binder = binder;
        this.objectTree = objectTree;
        Index = 0;
    }

    public void ResetIndex() => Index = 0;

    public override UINode Left() {
        var v = values();
        if (v.Length > 0) {
            Index = M.Mod(v.Length, Index - 1);
            onChange(v[Index]);
        }
        AssignValueText();
        return this;
    }
    public override UINode Right() {
        var v = values();
        if (v.Length > 0) {
            Index = M.Mod(v.Length, Index + 1);
            onChange(v[Index]);
        }
        AssignValueText();
        return this;
    }

    private void AssignValueText() {
        var v = values();
        if (v.Length != boundChildren.Length) Index = 0;
        foreach (var bc in boundChildren) childContainer.Remove(bc);
        boundChildren = v.Select((x, i) => {
            VisualElement t = objectTree.CloneTree();
            childContainer.Add(t);
            binder(x, t, i == Index);
            return t;
        }).ToArray();
    }

    private VisualElement childContainer = null!;
    protected override void BindText() {
        childContainer = bound.Q("LR2ChildContainer");
        bound.Q<Label>("Key").text = Description;
        AssignValueText();
    }
}

public class DynamicOptionNodeLR<T> : UINode, IOptionNodeLR {
    private readonly Action<T> onChange;
    private readonly Func<(LocalizedString key, T val)[]> values;
    private int index;
    public int Index {
        get { return index = M.Clamp(0, values().Length - 1, index); }
        set => index = value;
    }
    public T Value => values()[Index].val;

    public void SetIndexFromVal(T val) {
        index = this.values().Enumerate().FirstOrDefault(x => x.Item2.val!.Equals(val)).Item1;
    }
    public DynamicOptionNodeLR(LocalizedString description, Action<T> onChange, Func<(LocalizedString, T)[]> values, T defaulter, params UINode[] children) : base(description, children) {
        this.onChange = onChange;
        this.values = values;
        SetIndexFromVal(defaulter);
    }

    public override UINode Left() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index - 1);
            onChange(v[index].val);
            AssignValueText();
        }
        return this;
    }
    public override UINode Right() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index + 1);
            onChange(v[index].val);
            AssignValueText();
        }
        return this;
    }

    protected override void BindText() {
        bound.Q<Label>("Key").text = Description;
        AssignValueText();
    }
    private void AssignValueText() {
        bound.Q<Label>("Value").text = values()[Index].key;
    }
}
public class OptionNodeLR<T> : UINode, IOptionNodeLR {
    private readonly Action<T> onChange;
    protected virtual Action<T> OnChange => onChange;
    private readonly (LocalizedString key, T val)[] values;
    public int Index { get; set; }
    public T Value => values[Index].val;

    public void SetIndexFromVal(T val) {
        Index = this.values.Enumerate().First(x => 
            (x.Item2.val == null && val == null) || 
            x.Item2.val!.Equals(val)).Item1;
    }
    public OptionNodeLR(LocalizedString description, Action<T> onChange, (LocalizedString, T)[] values, T defaulter, params UINode[] children) : base(description, children) {
        this.onChange = onChange;
        this.values = values;
        SetIndexFromVal(defaulter);
    }

    public OptionNodeLR(LocalizedString description, Action<T> onChange, (string, T)[] values, T defaulter,
        params UINode[] children) : this(description, onChange,
        values.Select(v => (new LocalizedString(v.Item1), v.Item2)).ToArray(), defaulter, children) { }
    
    public OptionNodeLR(LocalizedString description, Action<T> onChange, T[] values, T defaulter, params UINode[] children) : base(description, children) {
        this.onChange = onChange;
        this.values = values.Select(x => (new LocalizedString(x.ToString()), x)).ToArray();
        SetIndexFromVal(defaulter);
    }

    public override UINode Left() {
        Index = M.Mod(values.Length, Index - 1);
        AssignValueText();
        OnChange(Value);
        return this;
    }
    public override UINode Right() {
        Index = M.Mod(values.Length, Index + 1);
        AssignValueText();
        OnChange(Value);
        return this;
    }

    protected override void BindText() {
        bound.Q<Label>("Key").text = Description;
        AssignValueText();
    }
    private void AssignValueText() {
        bound.Q<Label>("Value").text = values[Index].key;
    }
}

public class TextInputNode : UINode {
    public string DataWIP { get; private set; } = "";
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => DataWIP.Insert(bdCursorIdx, "|");

    public TextInputNode(LocalizedString title) : base((Func<LocalizedString>) null!) {
        descriptor = () => new LocalizedString($"{title}: {DisplayWIP}");
    }

    private static readonly string[] alphanumeric = 
        "abcdefghijklmnopqrstuvwxyz0123456789".Select(x => x.ToString()).ToArray();
    
    public override UINode? CustomEventHandling() {
        foreach (var kc in alphanumeric) {
            if (Input.GetKeyDown(kc)) {
                DataWIP = DataWIP.Insert(bdCursorIdx, (Input.GetKey(KeyCode.LeftShift) || 
                                                       Input.GetKey(KeyCode.RightShift)) ? kc.ToUpper() : kc);
                ++cursorIdx;
                return this;
            }
        }
        if (Input.GetKeyDown(KeyCode.Backspace)) {
            DataWIP =
                ((cursorIdx > 1) ? DataWIP.Substring(0, cursorIdx - 1) : "") +
                ((cursorIdx < DataWIP.Length) ? DataWIP.Substring(cursorIdx) : "");
            cursorIdx = Math.Max(0, cursorIdx - 1);
            return this;
        } else if (Input.GetKeyDown(KeyCode.Return)) {
            return Parent;
        } else return null;
    }

    public override UINode Left() {
        cursorIdx = Math.Max(0, cursorIdx - 1);
        return this;
    }
    public override UINode Right() {
        cursorIdx = Math.Min(DataWIP.Length, cursorIdx + 1);
        return this;
    }
    
}

}