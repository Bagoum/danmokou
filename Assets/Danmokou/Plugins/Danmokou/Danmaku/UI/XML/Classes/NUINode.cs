using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
public enum UINodeState {
    /// <summary>
    /// The cursor is on the node and it is in a special state that
    ///  permits some sort of functionality (such as modification of an OptionLR node).
    /// </summary>
    Active,
    /// <summary>
    /// The cursor is on the node.
    /// </summary>
    Focused,
    /// <summary>
    /// The node is in the same group as the Focused or Active node.
    /// </summary>
    GroupFocused,
    /// <summary>
    /// The node is in the GroupCall stack.
    /// </summary>
    GroupCaller,
    /// <summary>
    /// Default state (visible).
    /// </summary>
    Default
}
public record NUINode(Func<LString> Description) {
    public UIGroup Group { get; set; } = null!;
    /// <summary>
    /// The VisualElement constructed by this node. Usually points to a TemplateContainer.
    /// </summary>
    public VisualElement HTML { get; private set; } = null!;
    /// <summary>
    /// The .node VisualElement constructed by this node. Usually points to the only child of HTML, which should have the class .node.
    /// </summary>
    public VisualElement NodeHTML { get; private set; } = null!;
    /// <summary>
    /// Parent of HTML. Either Render.HTML or a descendant of Render.HTML.
    /// </summary>
    private VisualElement ContainerHTML { get; set; } = null!;
    /// <summary>
    /// The top-level descriptor label for this node.
    /// </summary>
    protected Label? Label { get; set; }

    public bool IsEnabled { get; private set; } = true;
    
    #region InitOptions

    /// <summary>
    /// Set whether or not this node should be "skipped over" for navigation events. Defaults to False.
    /// </summary>
    public bool Passthrough { get; init; } = false;
    /// <summary>
    /// Provide a function that determines whether or not the node is "enabled". A disabled node will
    ///  not allow confirm or edit operations. By default, a node is always enabled.
    /// </summary>
    public Func<bool>? EnabledWhen { get; init; }
    /// <summary>
    /// Given the HTML of the RenderSpace, select the object under which to construct this node's HTML.
    /// <br/>If not overriden, uses h => h.
    /// </summary>
    public Func<VisualElement, VisualElement>? BuildTarget { get; init; }
    /// <summary>
    /// Provide handling for styling the node or binding text when it is redrawn.
    /// </summary>
    public Action<UINodeState, NUINode>? InlineStyle { get; init; }
    /// <summary>
    /// Overrides the visualTreeAsset used to construct this node's HTML.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }
    /// <summary>
    /// Called after the HTML is built.
    /// </summary>
    public Action<NUINode>? OnBuilt { get; init; }
    /// <summary>
    /// Called when this node gains focus.
    /// </summary>
    public Action<NUINode>? OnEnter { get; init; }
    /// <summary>
    /// Called when this node loses focus.
    /// </summary>
    public Action<NUINode>? OnLeave { get; init; }
    /// <summary>
    /// Overrides Navigate, but if it returns null, then calls Navigate to provide the final result.
    /// </summary>
    public Func<UICommand, UIResult?>? Navigator { get; init; }
    /// <summary>
    /// A UIGroup to show on entry and hide on exit.
    /// </summary>
    public UIGroup? ShowHideGroup { get; init; }
    
    #endregion
    
    private string[] boundClasses = null!;
    
    //Keeping this old-style for porting simplicity
    private readonly List<string> overrideClasses = new();
    public NUINode With(params string?[] clss) {
        foreach (var cls in clss) {
            if (!string.IsNullOrWhiteSpace(cls)) overrideClasses.Add(cls!);
        }
        return this;
    }
    
    public UIRenderSpace Render => Group.Render;
    public NUIScreen Screen => Group.Screen;
    public UIController Controller => Group.Controller;
    
    
    public NUINode(LString description) : this(() => description) { }

    #region Construction
    protected virtual void RegisterEvents() {
        NodeHTML.RegisterCallback<MouseEnterEvent>(evt => {
            //Logs.Log($"Enter {Description}");
            Controller.QueuedEvent = new UIMouseCommand.Goto(this);
            evt.StopPropagation();
        });
        NodeHTML.RegisterCallback<MouseUpEvent>(evt => {
            //Logs.Log($"Click {Description}");
            //button 0, 1, 2 = left, right, middle click
            Controller.QueuedEvent = new UIMouseCommand.NormalCommand(
                evt.button == 1 ? UICommand.Back : UICommand.Confirm, this);
            evt.StopPropagation();
        });
    }
    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        HTML = (Prefab != null ? Prefab : map.SearchByType(this, true)).CloneTree();
        NodeHTML = HTML.Q<VisualElement>(null!, nodeClass);
        Label = NodeHTML.Query<Label>().ToList().FirstOrDefault();
        boundClasses = NodeHTML.GetClasses().ToArray();
        (ContainerHTML = BuildTarget?.Invoke(Render.HTML) ?? Render.HTML).Add(HTML);
        if (!Passthrough)
            RegisterEvents();
        OnBuilt?.Invoke(this);
    }
    public void TearDown() => ContainerHTML.Remove(HTML);
    
    #endregion

    #region Drawing

    public void ScrollTo() {
        NodeHTML.Focus();
        if (ContainerHTML is ScrollView sv)
            sv.ScrollTo(HTML);
    }
    protected virtual void Rebind() {
        if (Label != null)
            Label.text = Description();
    }
    public void Redraw(UINodeState state) {
        NodeHTML.ClearClassList();
        foreach (var c in boundClasses)
            NodeHTML.AddToClassList(c);
        NodeHTML.AddToClassList(!Group.Visible ? "invisible" : state switch {
            UINodeState.Active => "focus",
            UINodeState.Focused => "focus",
            UINodeState.GroupFocused => "group",
            UINodeState.GroupCaller => "selected",
            UINodeState.Default => "visible",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        });
        //Active receives .focus.active
        if (Group.Visible && state == UINodeState.Active)
            NodeHTML.AddToClassList("active");
        foreach (var cls in overrideClasses)
            NodeHTML.AddToClassList(cls);
        InlineStyle?.Invoke(state, this);
        if (!(IsEnabled = EnabledWhen?.Invoke() ?? true))
            NodeHTML.AddToClassList(disabledClass);
        Rebind();
    }
    
    #endregion
    
    #region Navigation

    /// <summary>
    /// Proceed with standard navigation iff this returns null.
    /// </summary>
    public virtual UIResult? CustomEventHandling() => null;

    /// <summary>
    /// Provided an input, modify the state of the UI appropriately, and return instructions for
    ///  control flow modification.
    /// </summary>
    public UIResult Navigate(UICommand req) => Navigator?.Invoke(req) ?? NavigateInternal(req);

    protected virtual UIResult NavigateInternal(UICommand req) => Group.Navigate(this, req);

    public void Enter(bool animate) {
        //TODO: handle this via options, eg. OnEnterAnim = new[] { ScaleBop(1.03, 0.1, 0.13), LocationTo(-100, 10)... }
        if (animate) {
            NodeHTML.transform.ScaleTo(1.03f, 0.1f, Easers.EOutSine)
                .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f))
                .Run(Controller);
        }
        ShowHideGroup?.Show();
        OnEnter?.Invoke(this);
    }

    public virtual void Leave(bool animate) {
        ShowHideGroup?.Hide();
        OnLeave?.Invoke(this);
    }
    
    #endregion
}

public record NTwoLabelUINode : NUINode {
    private readonly Func<LString> desc2;
    public NTwoLabelUINode(LString description1, Func<LString> description2) : base(() => description1) {
        desc2 = description2;
    }

    protected override void Rebind() {
        base.Rebind();
        NodeHTML.Q<Label>("Label2").text = desc2();
    }
}
public record FuncUINode(Func<LString> Description, Func<UIResult> Command) : NUINode(Description) {
    public FuncUINode(LString description, Func<UIResult> command) : this(() => description, command) { }
    protected override UIResult NavigateInternal(UICommand req) {
        if (req == UICommand.Confirm)
            return Command();
        return base.NavigateInternal(req);
    }
}

public record OpenURLUINode : FuncUINode {
    public OpenURLUINode(LString Description, string URL) : base(() => Description, () => {
        Application.OpenURL(URL);
        return new UIResult.StayOnNode(false);
    }) { }
}

public record TransferUINode : FuncUINode {
    public TransferUINode(LString description, UIGroup target) : 
        base(description, () => new UIResult.GoToNode(target)) { }
    public TransferUINode(LString description, UIScreen target) : 
        base(description, () => new UIResult.GoToNode(target.Groups[0])) { }
}

public record ConfirmFuncUINode(Func<LString> Description, Func<UIResult> Command) : NUINode(Description) {
    private bool isConfirm = false;
    
    public ConfirmFuncUINode(LString description, Func<UIResult> command) : this(() => description, command) { }

    protected override void Rebind() {
        if (Label != null)
            Label.text = isConfirm ? LocalizedStrings.UI.are_you_sure : Description();
    }

    protected override UIResult NavigateInternal(UICommand req) {
        if (req == UICommand.Confirm)
            // ReSharper disable once AssignmentInConditionalExpression
            return (isConfirm = !isConfirm) ? new UIResult.StayOnNode(false) : Command();
        return base.NavigateInternal(req);
    }

    public override void Leave(bool animate) {
        isConfirm = false;
        base.Leave(animate);
    }
}

public abstract record BaseLROptionUINode<T>(Func<LString> Description, Action<T> OnChange) : NUINode(Description) {
    protected int index;
    public abstract int Index { get; }
    public abstract T Value { get; }

    protected override void RegisterEvents() {
        base.RegisterEvents();
        NodeHTML.Q("Left").RegisterCallback<MouseUpEvent>(evt => {
            Controller.QueuedEvent =  new UIMouseCommand.NormalCommand(UICommand.Left, this);
            evt.StopPropagation();
        });
        NodeHTML.Q("Right").RegisterCallback<MouseUpEvent>(evt => {
            Controller.QueuedEvent = new UIMouseCommand.NormalCommand(UICommand.Right, this);
            evt.StopPropagation();
        });
    }

    protected abstract UIResult Left();
    protected abstract UIResult Right();

    protected override UIResult NavigateInternal(UICommand req) => req switch {
        UICommand.Left => Left(),
        UICommand.Right => Right(),
        _ => base.NavigateInternal(req)
    };
}

public record LROptionUINode<T> : BaseLROptionUINode<T> {
    private readonly Func<(LString key, T val)[]> values;
    public override int Index => index = M.Clamp(0, values().Length - 1, index);
    
    public override T Value => values()[Index].val;

    public LROptionUINode(Func<LString> description, Action<T> onChange, Func<(LString, T)[]> values, T defaulter) : base(description, onChange) {
        this.values = values;
        this.index = values().Enumerate().First(x => Equals(x.val, defaulter)).idx;

    }

    public LROptionUINode(Func<LString> description, Action<T> onChange, (LString, T)[] values, T defaulter) :
        this(description, onChange, () => values, defaulter) { }
    public LROptionUINode(LString description, Action<T> onChange, (LString, T)[] values, T defaulter) :
        this(() => description, onChange, () => values, defaulter) { }


    protected override void Rebind() {
        base.Rebind();
        NodeHTML.Q<Label>("Key").text = Description();
        NodeHTML.Q<Label>("Value").text = values()[Index].key;
    }

    protected override UIResult Left() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index - 1);
            OnChange(v[index].val);
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index + 1);
            OnChange(v[index].val);
        }
        return new UIResult.StayOnNode();
    }
}


public record ComplexLROptionUINode<T> : BaseLROptionUINode<T> {
    private readonly VisualTreeAsset objectTree;
    private readonly Action<T, VisualElement, bool> binder;
    private readonly Func<T[]> values;
    public override int Index => index = M.Clamp(0, values().Length - 1, index);
    
    public override T Value => values()[Index];

    public ComplexLROptionUINode(LString description, VisualTreeAsset objectTree, Action<T> onChange, Func<T[]> values, Action<T, VisualElement, bool> binder) : 
        base(() => description, onChange) {
        this.values = values;
        this.objectTree = objectTree;
        this.binder = binder;
        this.index = 0;
    }


    protected override void Rebind() {
        base.Rebind();
        NodeHTML.Q<Label>("Key").text = Description();
        NodeHTML.Q("LR2ChildContainer").Clear();
        foreach (var (i, v) in values().Enumerate()) {
            var ve = objectTree.CloneTree();
            NodeHTML.Q("LR2ChildContainer").Add(ve);
            binder(v, ve, i == index);
        }
    }

    protected override UIResult Left() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index - 1);
            OnChange(v[index]);
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index + 1);
            OnChange(v[index]);
        }
        return new UIResult.StayOnNode();
    }
}

public record TextInputUINode : NUINode {
    public string DataWIP { get; private set; } = "";
    private bool isEntryEnabled = false;
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => isEntryEnabled ? DataWIP.Insert(bdCursorIdx, "|") : DataWIP;

    public TextInputUINode(LString title) : base(title) {
        Description = () => new LString($"{title}: {DisplayWIP}");
    }

    private static readonly string[] alphanumeric = 
        "abcdefghijklmnopqrstuvwxyz0123456789".Select(x => x.ToString()).ToArray();
    
    public override UIResult? CustomEventHandling() {
        if (!isEntryEnabled) return null;
        foreach (var kc in alphanumeric)
            if (Input.GetKeyDown(kc)) {
                DataWIP = DataWIP.Insert(bdCursorIdx, (Input.GetKey(KeyCode.LeftShift) || 
                                                       Input.GetKey(KeyCode.RightShift)) ? kc.ToUpper() : kc);
                ++cursorIdx;
                return new UIResult.StayOnNode();
            }
        if (Input.GetKeyDown(KeyCode.Backspace)) {
            DataWIP =
                ((cursorIdx > 1) ? DataWIP.Substring(0, cursorIdx - 1) : "") +
                ((cursorIdx < DataWIP.Length) ? DataWIP.Substring(cursorIdx) : "");
            cursorIdx = Math.Max(0, cursorIdx - 1);
            return new UIResult.StayOnNode();
        } else if (Input.GetKeyDown(KeyCode.Return)) {
            return EnableDisableEntry();
        } else return null;
    }

    public UIResult Left() {
        cursorIdx = Math.Max(0, cursorIdx - 1);
        return new UIResult.StayOnNode();
    }
    public UIResult Right() {
        cursorIdx = Math.Min(DataWIP.Length, cursorIdx + 1);
        return new UIResult.StayOnNode();
    }

    private UIResult EnableDisableEntry() {
        isEntryEnabled = !isEntryEnabled;
        return new UIResult.StayOnNode();
    }
    
    //This is only reached if custom handling does nothing
    protected override UIResult NavigateInternal(UICommand req) => req switch {
        UICommand.Left => Left(),
        UICommand.Right => Right(),
        UICommand.Confirm => EnableDisableEntry(),
        _ => base.NavigateInternal(req)
    };
}

}