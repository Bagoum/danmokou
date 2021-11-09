using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Core;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public enum NodeState {
    /// <summary>
    /// The cursor is on the node. Only one node per screen may be in this state at once.
    /// </summary>
    Focused,
    /// <summary>
    /// This node is a parent of the focused node.
    /// </summary>
    Selected,
    /// <summary>
    /// This node is a sibling of the focused node.
    /// </summary>
    GroupFocused,
    /// <summary>
    /// This node is visible.
    /// </summary>
    Visible,
    /// <summary>
    /// This node is not visible.
    /// </summary>
    Invisible
}



public class UINode {
    public readonly UINode[] children;
    public UINode? Parent { get; private set; }
    public UINode[] Siblings { get; set; } = null!; //including self

    public LString Description => descriptor();
    //sorry but there's a use case where i need to modify this in the initializer. see TextInputNode
    protected Func<LString> descriptor;
    public UIScreen screen = null!;
    private XMLMenu Container => screen.Container;
    public NodeState state = NodeState.Invisible;

    public UINode(LString description, params UINode[] children) : this(() => description, children) { }
    public UINode(Func<LString> descriptor, params UINode[] children) {
        this.children = children;
        this.descriptor = descriptor;
        foreach (var c in children) {
            c.Parent = this;
            c.Siblings = children;
        }
    }

    public IEnumerable<UINode> ListAll() => children.SelectMany(n => n.ListAll()).Prepend(this);

    public int Depth => (Parent?.Depth ?? -1) + 1;

    protected static void AssignParentingStates(UINode? p) {
        for (; p != null; p = p.Parent) {
            foreach (var x in p.Siblings) x.state = NodeState.Visible;
            p.state = NodeState.Selected;
        }
    }
    protected virtual void AssignParentStatesFromSelected() => AssignParentingStates(Parent);
    public virtual void AssignStatesFromSelected() {
        screen.ResetStates();
        foreach (var x in children) x.state = NodeState.Visible;
        foreach (var x in Siblings) x.state = NodeState.GroupFocused;
        this.state = NodeState.Focused;
        AssignParentStatesFromSelected();
        screen.ApplyStates();
    }

    private readonly List<string> overrideClasses = new();
    private readonly List<Action<VisualElement>> onBind = new();
    private readonly List<Action<NodeState, VisualElement>> overrideInline = new();

    public UINode With(params string?[] clss) {
        foreach (var cls in clss) {
            if (!string.IsNullOrWhiteSpace(cls)) overrideClasses.Add(cls!);
        }
        return this;
    }

    public UINode OnBound(Action<VisualElement>? func) {
        if (func != null)
            onBind.Add(func);
        return this;
    }
    public UINode With(Action<VisualElement>? func) {
        if (func != null) overrideInline.Add((_, x) => func(x));
        return this;
    }
    public UINode With(Action<NodeState, VisualElement>? func) {
        if (func != null) overrideInline.Add(func);
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
            inline(state, boundNode);
        }
        confirmEnabled = (enableCheck?.Invoke() ?? true);
        if (!confirmEnabled) boundNode.AddToClassList(disabledClass);
    }

    private bool confirmEnabled = true; //otherwise doesn't work with ReturnTo

    private Func<UINode>? _overrideRight;
    public UINode SetRightOverride(Func<UINode> overr) {
        _overrideRight = overr;
        return this;
    }
    public UINode SetChildrenInaccessible() => SetRightOverride(() => this);

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
                                  Siblings.ModIndex(Siblings.IndexOf(this) - 1);
    
    private Func<UINode>? _overrideDown;
    public UINode SetDownOverride(Func<UINode> overr) {
        _overrideDown = overr;
        return this;
    }
    public virtual UINode Down() => _overrideDown?.Invoke() ?? 
                                    Siblings.ModIndex(Siblings.IndexOf(this) + 1);

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
    public void OnVisit(UINode prev, bool animate) {
        if (animate) {
            boundNode.transform.ScaleTo(1.03f, 0.1f, Easers.EOutSine)
                .Then(() => boundNode.transform.ScaleTo(1f, 0.13f))
                .Run(Container);
        }
        _onVisit?.Invoke(prev);
    }
    public UINode SetOnLeave(Action<UINode?> onLeave) {
        _onLeave = onLeave;
        return this;
    }
    public virtual void OnLeave(UINode? nxt) {
        _onLeave?.Invoke(nxt);
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

    protected VisualElement bound = null!;
    protected VisualElement boundNode = null!;
    private VisualElement htmlContainer = null!;
    private string[] boundClasses = new string[0];
    private Label? boundLabel;
    protected Label BoundLabel => boundLabel ??= bound.Q<Label>();

    public void ScrollTo() {
        boundNode.Focus();
        if (htmlContainer is ScrollView sv)
            sv.ScrollTo(bound);
    }

    protected virtual void BindText() => BoundLabel.text = Description;

    private Func<VisualElement, VisualElement>? _buildWith;

    public UINode BuildWith(Func<VisualElement, VisualElement> buildWith) {
        _buildWith = buildWith;
        return this;
    }

    public UINode BuildWith<T>() where T: VisualElement => BuildWith(h => h.Q<T>());

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map, VisualElement htmlContainer_) {
        CloneTree(map);
        BindText();
        (this.htmlContainer = _buildWith?.Invoke(htmlContainer_) ?? htmlContainer_).Add(bound);
        return bound;
    }

    protected void CloneTree(Dictionary<Type, VisualTreeAsset> map) {
        bound = (overrideBuilder == null ? map.SearchByType(this, true) : overrideBuilder).CloneTree();
        boundNode = bound.Q<VisualElement>(null!, NodeClass);
        foreach (var f in onBind)
            f(boundNode);
        boundClasses = boundNode.GetClasses().ToArray();
        if (!Passthrough) {
            boundNode.RegisterCallback<MouseEnterEvent>(evt => {
                //Logs.Log($"Enter {Description}");
                Container.QueuedEvent = (this, QueuedEvent.Goto);
                evt.StopPropagation();
            });
            boundNode.RegisterCallback<MouseUpEvent>(evt => {
                //Logs.Log($"Click {Description}");
                //button 0, 1, 2 = left, right, middle click
                Container.QueuedEvent = (this, evt.button == 1 ? QueuedEvent.Back : QueuedEvent.Confirm);
                evt.StopPropagation();
            });
            if (this is IOptionNodeLR || this is IComplexOptionNodeLR) {
                boundNode.Q("Left").RegisterCallback<MouseUpEvent>(evt => {
                    Container.QueuedEvent = (this, QueuedEvent.Left);
                    evt.StopPropagation();
                });
                boundNode.Q("Right").RegisterCallback<MouseUpEvent>(evt => {
                    Container.QueuedEvent = (this, QueuedEvent.Right);
                    evt.StopPropagation();
                });
            }
        }
        //TODO: you can use a callback like this to replace a lot of the manual handling.
        //bound.RegisterCallback<KeyDownEvent>(evt => Debug.Log($"{Description}: {evt.keyCode} {evt.target}"));
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
    private readonly Func<LString> desc2;
    public TwoLabelUINode(Func<LString> desc1, Func<LString> desc2, params UINode[] children) :
        base(desc1, children) {
        this.desc2 = desc2;

    }
    public TwoLabelUINode(LString desc1, Func<LString> desc2, params UINode[] children) :
        base(desc1, children) {
        this.desc2 = desc2;

    }

    protected override void BindText() {
        bound.Q<Label>("Label2").text = desc2();
        base.BindText();
    }
}

public class NavigateUINode : UINode {
    
    public NavigateUINode(LString description, params UINode[] children) : base(description, children) {}
    public NavigateUINode(Func<LString> description, params UINode[] children) : base(description, children) {}
    protected override (bool success, UINode? target) _Confirm() {
        //default going right
        var n = Right();
        return n != this ? (true, n) : base._Confirm();
    }
}

public class CacheNavigateUINode : NavigateUINode {
    private readonly Action<List<XMLMenu.CacheInstruction>> cacher;
    public CacheNavigateUINode(Action<List<XMLMenu.CacheInstruction>> cacher, LString description, params UINode[] children) :
        base(description, children) {
        this.cacher = cacher;
    }
    public CacheNavigateUINode(Action<List<XMLMenu.CacheInstruction>> cacher, Func<LString> description, params UINode[] children) :
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

    public TransferNode(UIScreen target, LString description, params UINode[] children) : base(description, children) {
        this.screen_target = target;
    }

    protected override (bool success, UINode target) _Confirm() {
        return (true, screen_target.First);
    }
}

public class FuncNode : UINode {

    protected readonly Func<bool> target;
    protected readonly UINode? next;

    public FuncNode(Func<bool> target, LString description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }
    public FuncNode(Func<bool> target, Func<LString> description, bool returnSelf=false, params UINode[] children) : base(description, children) {
        this.target = target;
        this.next = returnSelf ? this : null;
    }

    public FuncNode(Action target, LString description, bool returnSelf = false, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, returnSelf, children) { }
    public FuncNode(Action target, Func<LString> description, bool returnSelf = false, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, returnSelf, children) { }

    public FuncNode(Action target, LString description, UINode next, params UINode[] children) : this(() => {
        target();
        return true;
    }, description, true, children) {
        this.next = next;
    }

    protected override (bool success, UINode? target) _Confirm() => (target(), next);
}

public class OpenUrlNode : FuncNode {

    public OpenUrlNode(string site, LString description) : base(() => Application.OpenURL(site), description, true) { }
}

public class ConfirmFuncNode : FuncNode {
    private bool isConfirm;
    public ConfirmFuncNode(Action target, LString description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }
    public ConfirmFuncNode(Action target, Func<LString> description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }
    public ConfirmFuncNode(Func<bool> target, LString description, bool returnSelf=false, params UINode[] children) : base(target, description, returnSelf, children) { }

    private void SetConfirm(bool newConfirm) {
        isConfirm = newConfirm;
    }
    protected override void BindText() {
        BoundLabel.text = isConfirm ? LocalizedStrings.UI.are_you_sure : Description;
    }

    public override void OnLeave(UINode? nxt) {
        SetConfirm(false);
        base.OnLeave(nxt);
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
    public PassthroughNode(LString description) : base(description) {
        PassthroughIf(() => true);
    }
    public PassthroughNode(Func<LString> description) : base(description) { 
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
    
    public DynamicComplexOptionNodeLR(LString description, VisualTreeAsset objectTree, Action<T> onChange, Func<T[]> values, Action<T, VisualElement, bool> binder) : base(description) {
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
    private readonly Func<(string key, T val)[]> values;
    private int index;
    public int Index {
        get { return index = M.Clamp(0, values().Length - 1, index); }
        set => index = value;
    }
    public T Value => values()[Index].val;

    public void SetIndexFromVal(T val) {
        index = this.values().Enumerate().FirstOrDefault(x => x.Item2.val!.Equals(val)).Item1;
    }
    public DynamicOptionNodeLR(LString description, Action<T> onChange, Func<(string, T)[]> values, T defaulter, params UINode[] children) : base(description, children) {
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
        KeyLabel.text = Description;
        AssignValueText();
    }
    private void AssignValueText() {
        ValLabel.text = values()[Index].key;
    }

    private Label? keyLabel;
    private Label KeyLabel => keyLabel ??= bound.Q<Label>("Key");
    private Label? valLabel;
    private Label  ValLabel => valLabel ??= bound.Q<Label>("Value");
}
public class OptionNodeLR<T> : UINode, IOptionNodeLR {
    private readonly Action<T> onChange;
    protected virtual Action<T> OnChange => onChange;
    private readonly (LString key, T val)[] values;
    public int Index { get; set; }
    public T Value => values[Index].val;

    public void SetIndexFromVal(T val) {
        Index = this.values.Enumerate().First(x => 
            (x.Item2.val == null && val == null) || 
            x.Item2.val?.Equals(val) == true).Item1;
    }
    public OptionNodeLR(LString description, Action<T> onChange, (LString, T)[] values, T defaulter, params UINode[] children) : base(description, children) {
        this.onChange = onChange;
        this.values = values;
        SetIndexFromVal(defaulter);
    }

    public OptionNodeLR(LString description, Action<T> onChange, (string, T)[] values, T defaulter,
        params UINode[] children) : this(description, onChange,
        values.Select(v => (new LString(v.Item1), v.Item2)).ToArray(), defaulter, children) { }
    
    public OptionNodeLR(LString description, Action<T> onChange, T[] values, T defaulter, params UINode[] children) : base(description, children) {
        this.onChange = onChange;
        this.values = values.Select(x => (new LString(x?.ToString() ?? "Null"), x)).ToArray();
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
        KeyLabel.text = Description;
        AssignValueText();
    }
    private void AssignValueText() {
        ValLabel.text = values[Index].key;
    }

    private Label? keyLabel;
    private Label KeyLabel => keyLabel ??= bound.Q<Label>("Key");
    private Label? valLabel;
    private Label  ValLabel => valLabel ??= bound.Q<Label>("Value");
}

public class TextInputNode : UINode {
    public string DataWIP { get; private set; } = "";
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => DataWIP.Insert(bdCursorIdx, "|");

    public TextInputNode(LString title) : base((Func<LString>) null!) {
        descriptor = () => new LString($"{title}: {DisplayWIP}");
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