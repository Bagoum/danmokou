using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
/// <summary>
/// An enum describing the visibility of a node based on its functional state.
/// <br/>Note that this enum does not describe the functional state of the node, just the visibility.
/// </summary>
public enum UINodeVisibility {
    /// <summary>
    /// The cursor is on the node and it is in a special state that
    ///  permits some sort of functionality (such as modification of an OptionLR node).
    /// </summary>
    Active = 4,
    /// <summary>
    /// The cursor is on the node.
    /// </summary>
    Focused = 3,
    /// <summary>
    /// The node is in the same group as the Focused or Active node.
    /// </summary>
    GroupFocused = 2,
    /// <summary>
    /// The node is in the GroupCall stack.
    /// </summary>
    GroupCaller = 1,
    /// <summary>
    /// Default state (visible).
    /// </summary>
    Default = 0
}
public class UINode {
    public Func<LString> Description { get; init; }
    private UIGroup _group = null!;
    public UIGroup Group {
        get => _group;
        set {
            _group = value;
            if (ShowHideGroup != null)
                ShowHideGroup.Parent = _group;
        }
    }
    /// <summary>
    /// The VisualElement constructed by this node. Usually points to a TemplateContainer.
    /// Currently operating under the assumption that there are no templateContainer wrappers.
    /// </summary>
    public VisualElement HTML => NodeHTML;
    public IStyle Style => HTML.style;
    /// <summary>
    /// The .node VisualElement constructed by this node. Usually points to the only child of HTML, which should have the class .node.
    /// </summary>
    public VisualElement NodeHTML { get; private set; } = null!;
    public VisualElement BodyHTML => NodeHTML.Q("Body");

    /// <summary>
    /// Parent of HTML. Either Render.HTML or a descendant of Render.HTML.
    /// </summary>
    private VisualElement ContainerHTML { get; set; } = null!;
    /// <summary>
    /// The top-level descriptor label for this node.
    /// </summary>
    public Label? Label { get; set; }

    public bool IsEnabled { get; private set; } = true;
    
    /// <summary>
    /// Cached result of VisibleIf.
    /// </summary>
    private bool _visible = true;
    
    public bool AllowInteraction => (Passthrough != true) && Group.Interactable && _visible;
    public int IndexInGroup => Group.Nodes.IndexOf(this);
    public bool Destroyed { get; private set; } = false;
    
    #region InitOptions

    /// <summary>
    /// Set whether or not this node should be "skipped over" for navigation events.
    /// <br/>Defaults to null, which operates like False, but can be overriden by the group.
    /// <br/>If the node is not visible (due to VisibleIf returning false), then it will also be skipped.
    /// </summary>
    public bool? Passthrough { get; set; } = null;
    /// <summary>
    /// Set whether this node should tentatively save its location in the controller ReturnTo cache when the user
    /// navigates to it.
    /// </summary>
    public bool CacheOnEnter { private get; init; } = false;
    /// <summary>
    /// Provide a function that determines whether or not the node is visible.
    ///  By default, a node is always visible.
    /// <br/>Note that this does not override group visibility; if a group is not visible,
    ///  none of its nodes will be visible.
    /// </summary>
    public Func<bool>? VisibleIf { private get; init; }
    /// <summary>
    /// Provide a function that determines whether or not the node is "enabled". A disabled node will
    ///  not allow confirm or edit operations, but can still be navigated. By default, a node is always enabled.
    /// </summary>
    public Func<bool>? EnabledIf { get; init; }
    /// <summary>
    /// Given the HTML of the RenderSpace, select the object under which to construct this node's HTML.
    /// <br/>If not overriden, uses h => h.
    /// </summary>
    public Func<VisualElement, VisualElement>? BuildTarget { get; set; }
    public UINode WithBuildTarget(Func<VisualElement, VisualElement>? bt) {
        BuildTarget = bt ?? BuildTarget;
        return this;
    }

    /// <summary>
    /// Provide handling for styling the node or binding text when it is redrawn.
    /// </summary>
    public Action<UINodeVisibility, UINode>? InlineStyle { get; set; }
    /// <summary>
    /// Overrides the visualTreeAsset used to construct this node's HTML.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }
    /// <summary>
    /// Called after the HTML is built.
    /// </summary>
    public Action<UINode>? OnBuilt { get; set; }
    /// <summary>
    /// Called when this node gains focus.
    /// </summary>
    public Action<UINode>? OnEnter { get; init; }
    /// <summary>
    /// Called when this node loses focus.
    /// </summary>
    public Action<UINode>? OnLeave { get; init; }
    
    /*
    /// <summary>
    /// Called when the mouse leaves the bounds of this node.
    /// <br/>Note that this does not mean the node has lost focus! Use <see cref="OnLeave"/>
    ///  to track when the node loses focus.
    /// </summary>
    public Action<UINode, PointerLeaveEvent>? OnMouseLeave { get; init; }*/
    
    /// <summary>
    /// Called when the mouse is pressed over this node.
    /// <br/>Note that this may not be followed by OnMouseUp (if the mouse moves outside the bounds
    ///  before being released).
    /// <br/>OnMouseEnter and OnMouseLeave are not provided as it is preferred to use OnEnter and OnLeave,
    ///  which are tied more closely to layout handling.
    /// </summary>
    public Action<UINode, PointerDownEvent>? OnMouseDown { get; init; }
    /// <summary>
    /// Called when the mouse is released over this node.
    /// <br/>Note that this may not be preceded by OnMouseDown.
    /// </summary>
    public Action<UINode, PointerUpEvent>? OnMouseUp { get; init; }
    
    /// <summary>
    /// Overrides Navigate, but if it returns null, then falls through to OnConfirm/Navigate to provide the final result.
    /// </summary>
    public Func<UINode, UICommand, UIResult?>? Navigator { get; init; }
    /// <summary>
    /// Overrides Navigate for Confirm entries. Runs after Navigator but before Navigate.
    /// </summary>
    public Func<UIResult>? OnConfirm { get; init; }
    private readonly UIGroup? _showHideGroup;
    /// <summary>
    /// A UIGroup to show on entry and hide on exit.
    /// </summary>
    public UIGroup? ShowHideGroup {
        get => _showHideGroup;
        init {
            if (value != null)
                value.Parent = Group;
            _showHideGroup = value;
        } 
    }
    /// <summary>
    /// List of CSS classes to apply to the node HTML.
    /// </summary>
    public List<string> OverrideClasses { get; init; } = new();

    public bool UseDefaultAnimations { get; set; } = true;
    
    #endregion
    
    private string[] boundClasses = null!;
    private Cancellable animToken = new();

    public UINode With(params string?[] clss) {
        foreach (var cls in clss) {
            if (!string.IsNullOrWhiteSpace(cls)) OverrideClasses.Add(cls!);
        }
        return this;
    }
    
    public UIRenderSpace Render => Group.Render;
    public UIScreen Screen => Group.Screen;
    public UIController Controller => Group.Controller;
    /// <summary>
    /// Creates a ReturnToTargetGroupCaller targeting this node's group.
    /// </summary>
    public UIResult ReturnGroup => new UIResult.ReturnToTargetGroupCaller(this);

    public UINode(Func<LString> description) {
        this.Description = description;
    }
    
    public UINode(LString description) : this(() => description) { }
    public UINode() : this(() => LString.Empty) { }

    #region Construction
    protected virtual void RegisterEvents() {
        bool isInElement = false;
        bool startedClickHere = false;
        NodeHTML.RegisterCallback<PointerEnterEvent>(evt => {
           //Logs.Log($"Enter {Description()} {evt.position} {evt.localPosition}");
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            //PointerEnter is still issued while there's no touch, at the last touched point
            if (evt.pressure <= 0)
                return;
        #endif
            if (AllowInteraction)
                Controller.QueuedEvent = new UIPointerCommand.Goto(this);
            evt.StopPropagation();
            isInElement = true;
        });
        NodeHTML.RegisterCallback<PointerLeaveEvent>(evt => {
            //Logs.Log($"Leave {Description()}");
            //For freeform groups ONLY, moving the cursor off a node should deselect it.
            if (AllowInteraction && Group is UIFreeformGroup)
                Controller.QueuedEvent = new UIPointerCommand.NormalCommand(UICommand.Back, this) { Silent = true };
            isInElement = false;
            startedClickHere = false;
        });
        NodeHTML.RegisterCallback<PointerDownEvent>(evt => {
            //Logs.Log($"Down {Description()}");
            if (AllowInteraction)
                OnMouseDown?.Invoke(this, evt);
            startedClickHere = true;
        });
        NodeHTML.RegisterCallback<PointerUpEvent>(evt => {
            //Logs.Log($"Click {Description()}");
            //button 0, 1, 2 = left, right, middle click
            //Right click is handled as UIBack in InputManager. UIBack is global (it does not depend
            // on the current UINode), but click-to-confirm is done via callbacks specific to the UINode.
            if (AllowInteraction && evt.button == 0) {
                if (isInElement && startedClickHere)
                    Controller.QueuedEvent = new UIPointerCommand.NormalCommand(UICommand.Confirm, this);
                OnMouseUp?.Invoke(this, evt);
            }
            evt.StopPropagation();
            startedClickHere = false;
        });
    }
    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        NodeHTML = (Prefab != null ? Prefab : map.SearchByType(this, true)).CloneTreeWithoutContainer();
        //NodeHTML = HTML.Q<VisualElement>(null!, nodeClass);
        Label = NodeHTML.Query<Label>().ToList().FirstOrDefault();
        boundClasses = NodeHTML.GetClasses().ToArray();
        (ContainerHTML = BuildTarget?.Invoke(Render.HTML) ?? Render.HTML).Add(HTML);
        if (Passthrough != true)
            RegisterEvents();
        OnBuilt?.Invoke(this);
    }

    public void Remove() {
        Destroyed = true;
        Group.Nodes.Remove(this);
        ContainerHTML.Remove(HTML);
        Controller.MoveCursorAwayFromNode(this);
    }

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

    private static UINodeVisibility Max(UINodeVisibility a, UINodeVisibility b) => a > b ? a : b;

    /// <summary>
    /// Cached result of whether or not this node rendered (ie. _visible && Group.Visible)
    /// </summary>
    private bool lastFrameRendered = true;
    public void Redraw(UINodeVisibility visibility) {
        _visible = VisibleIf?.Invoke() ?? true;
        //Don't do any HTML updates if the node is not rendered between both frames.
        //This makes structures such as save/load (~1/10 of nodes are rendered at a time) much more efficient.
        var thisFrameRender = _visible && Group.Visible;
        if (!lastFrameRendered && !thisFrameRender)
            return;
        
        lastFrameRendered = thisFrameRender;
        NodeHTML.ClearClassList();
        foreach (var c in boundClasses)
            NodeHTML.AddToClassList(c);
        //For inaccessible groups, we make them group-focused
        if (visibility >= UINodeVisibility.Default && !Group.HasInteractableNodes)
            visibility = Max(visibility, UINodeVisibility.GroupFocused);
        NodeHTML.AddToClassList(!thisFrameRender ? "invisible" : visibility switch {
            UINodeVisibility.Active => "focus",
            UINodeVisibility.Focused => "focus",
            UINodeVisibility.GroupFocused => "group",
            UINodeVisibility.GroupCaller => "selected",
            UINodeVisibility.Default => "visible",
            _ => throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null)
        });
        //Active receives .focus.active
        if (Group.Visible && visibility == UINodeVisibility.Active)
            NodeHTML.AddToClassList("active");
        foreach (var cls in OverrideClasses)
            NodeHTML.AddToClassList(cls);
        if (!(IsEnabled = EnabledIf?.Invoke() ?? true))
            NodeHTML.AddToClassList(disabledClass);
        Rebind();
        InlineStyle?.Invoke(visibility, this);
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
    public UIResult Navigate(UICommand req) {
        if (req == UICommand.Confirm && !IsEnabled) return new UIResult.StayOnNode(true);
        return Navigator?.Invoke(this, req) ?? req switch {
            UICommand.Confirm when OnConfirm != null => OnConfirm(),
            _ => NavigateInternal(req)
        };
    }

    protected virtual UIResult NavigateInternal(UICommand req) => Group.Navigate(this, req);

    public void Enter(bool animate) {
        if (CacheOnEnter) Controller.TentativeCache(this);
        //TODO: handle this via options, eg. OnEnterAnim = new[] { ScaleBop(1.03, 0.1, 0.13), LocationTo(-100, 10)... }
        if (animate) {
            var anims = EnterAnimations().ToList();
            if (anims.Count > 0) {
                animToken.SoftCancel();
                animToken = new();
                foreach (var anim in anims)
                    _ = anim(animToken);
            }
        }
        ShowHideGroup?.EnterShow();
        OnEnter?.Invoke(this);
        Group.EnteredNode(this, animate);
    }

    public virtual void Leave(bool animate) {   
        if (animate) {
            var anims = LeaveAnimations().ToList();
            if (anims.Count > 0) {
                animToken.SoftCancel();
                animToken = new();
                foreach (var anim in anims)
                    _ = anim(animToken);
            }
        }
        ShowHideGroup?.LeaveHide();
        OnLeave?.Invoke(this);
    }
    protected virtual IEnumerable<Func<ICancellee, Task>> EnterAnimations() {
        if (UseDefaultAnimations) {
            yield return cT => NodeHTML.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
                                .Then(() => NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
                                .Run(Controller, new CoroutineOptions(true));
        }
    }

    protected virtual IEnumerable<Func<ICancellee, Task>> LeaveAnimations() => 
        Array.Empty<Func<ICancellee, Task>>();

    #endregion
}

public class EmptyNode : UINode {
    public IFixedXMLObject Source { get; }
    private readonly string desc;
    public EmptyNode(IFixedXMLObject source, Action<EmptyNode>? onBuild = null) : base(source.Descriptor) {
        this.Source = source;
        this.desc = source.Descriptor;
        UseDefaultAnimations = false;
        Navigator = source.Navigate;
        OnBuilt = n => {
            _ = source.IsVisible.Subscribe(b => {
                Passthrough = !b;
                n.HTML.style.display = b.ToStyle();
                if (!b)
                    Controller.MoveCursorAwayFromNode(this);
            });

            n.HTML.ConfigureAbsoluteEmpty().ConfigureLeftTopListeners(source.Left, source.Top);
            source.Width.Subscribe(w => n.Style.width = w);
            source.Height.Subscribe(h => n.Style.height = h);
            
            //n.HTML.style.backgroundColor = new Color(0, 1, 0, 0.4f);
            onBuild?.Invoke(this);
        };
    }

    public ICObservable<float> CreateCenterOffsetChildX(ICObservable<float> childX) =>
        new LazyEvented<float>(() => childX.Value + Source.Width.Value / 2f,
            new UnitEventProxy<float>(childX), new UnitEventProxy<float>(Source.Width));
    
    public ICObservable<float> CreateCenterOffsetChildY(ICObservable<float> childY) =>
        new LazyEvented<float>(() => childY.Value + Source.Height.Value / 2f,
            new UnitEventProxy<float>(childY), new UnitEventProxy<float>(Source.Height));
}

public class PassthroughNode : UINode {
    public PassthroughNode(LString? desc = null) : base(desc ?? LString.Empty) {
        Passthrough = true;
    }
    public PassthroughNode(Func<LString> desc) : base(desc) {
        Passthrough = true;
    }
}
public class TwoLabelUINode : UINode {
    private readonly Func<LString> desc2;
    public TwoLabelUINode(LString description1, Func<LString> description2) : base(() => description1) {
        desc2 = description2;
    }
    public TwoLabelUINode(LString description1, LString description2) : this(description1, () => description2){ }
    public TwoLabelUINode(LString description1, object description2) : this(description1, description2.ToString()) { }

    protected override void Rebind() {
        base.Rebind();
        NodeHTML.Q<Label>("Label2").text = desc2();
    }
}
public class FuncNode : UINode {
    public Func<FuncNode, UIResult> Command { get; }

    public FuncNode(Func<LString> description, Func<FuncNode, UIResult> command) : base(description) {
        this.Command = command;
    }
    public FuncNode(LString description, Func<FuncNode, UIResult> command) : this(() => description, command) { }
    public FuncNode(Func<LString> description, Func<UIResult> command) : this(description, _ => command()) { }
    public FuncNode(LString description, Func<UIResult> command) : this(() => description, command) { }
    public FuncNode(Func<LString> description, Action command) : this(description, () => {
        command();
        return new UIResult.StayOnNode();
    }) { }
    public FuncNode(LString description, Action command) : this(() => description, command) { }
    public FuncNode(LString description, Func<bool> command) : this(() => description, () => new UIResult.StayOnNode(!command())) { }
    protected override UIResult NavigateInternal(UICommand req) {
        if (req == UICommand.Confirm)
            return Command(this);
        return base.NavigateInternal(req);
    }
}

public class OpenUrlNode : FuncNode {
    public OpenUrlNode(LString Description, string URL) : base(() => Description, () => {
        Application.OpenURL(URL);
        return new UIResult.StayOnNode(false);
    }) { }
}

public class TransferNode : FuncNode {
    public TransferNode(LString description, UIGroup target) : 
        base(description, () => new UIResult.GoToNode(target)) { }
    public TransferNode(LString description, UIScreen target) : 
        base(description, () => new UIResult.GoToNode(target.Groups[0])) { }
}

public class ConfirmFuncNode : UINode {
    private bool isConfirm = false;
    public Func<ConfirmFuncNode, UIResult> Command { get; }

    public ConfirmFuncNode(Func<LString> description, Func<ConfirmFuncNode, UIResult> command) : base(description) {
        this.Command = command;
    }
    public ConfirmFuncNode(Func<LString> description, Func<UIResult> command) : this(description, _ => command()) { }
    
    public ConfirmFuncNode(LString description, Func<UIResult> command) : this(() => description, command) { }
    public ConfirmFuncNode(LString description, Action command) : this(() => description, command) { }
    public ConfirmFuncNode(Func<LString> description, Action command) : this(description, () => {
        command();
        return new UIResult.StayOnNode();
    }) { }
    public ConfirmFuncNode(LString description, Func<bool> command) : this(() => description, 
        () => new UIResult.StayOnNode(!command())) { }

    protected override void Rebind() {
        if (Label != null)
            Label.text = isConfirm ? LocalizedStrings.UI.are_you_sure : Description();
    }

    protected override UIResult NavigateInternal(UICommand req) {
        if (req == UICommand.Confirm)
            // ReSharper disable once AssignmentInConditionalExpression
            return (isConfirm = !isConfirm) ? new UIResult.StayOnNode(false) : Command(this);
        return base.NavigateInternal(req);
    }

    public override void Leave(bool animate) {
        isConfirm = false;
        base.Leave(animate);
    }
}

public interface IBaseOptionNodeLR {
    int Index { get; set; }
}
//Separated for buildMap compatibilty
public interface IOptionNodeLR : IBaseOptionNodeLR {
}
public interface IComplexOptionNodeLR : IBaseOptionNodeLR {
}
public abstract class BaseLROptionUINode<T> : UINode {
    protected int index;
    public Action<T> OnChange { get; }

    public BaseLROptionUINode(Func<LString> Description, Action<T> OnChange) : base(Description) {
        this.OnChange = OnChange;
    }

    protected override void RegisterEvents() {
        base.RegisterEvents();
        NodeHTML.Q("Left").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueuedEvent =  new UIPointerCommand.NormalCommand(UICommand.Left, this);
            evt.StopPropagation();
        });
        NodeHTML.Q("Right").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueuedEvent = new UIPointerCommand.NormalCommand(UICommand.Right, this);
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

public class OptionNodeLR<T> : BaseLROptionUINode<T>, IOptionNodeLR {
    private readonly Func<(LString key, T val)[]> values;
    public int Index {
        get => index = M.Clamp(0, values().Length - 1, index);
        set => index = value;
    }

    public T Value => values()[Index].val;

    public void SetIndexFromVal(T val) {
        index = this.values().Enumerate().FirstOrDefault(x => Equals(x.val.val, val)).idx;
    }
    public OptionNodeLR(Func<LString> description, Action<T> onChange, Func<(LString, T)[]> values, T defaulter) : base(description, onChange) {
        this.values = values;
        SetIndexFromVal(defaulter);
    }

    public OptionNodeLR(Func<LString> description, Action<T> onChange, (LString, T)[] values, T defaulter) :
        this(description, onChange, () => values, defaulter) { }
    public OptionNodeLR(LString description, Action<T> onChange, (LString, T)[] values, T defaulter) :
        this(() => description, onChange, () => values, defaulter) { }
    public OptionNodeLR(LString description, Action<T> onChange, Func<(LString, T)[]> values, T defaulter) :
        this(() => description, onChange, values, defaulter) { }

    public OptionNodeLR(LString description, Action<T> onChange, T[] values, T defaulter) :
        this(() => description, onChange, () => values.Select(x => (default(LString)!, x)).ToArray(), defaulter) {
        var valuesAndStrs = values.Select(x => ((LString)(x?.ToString() ?? "Null"), x)).ToArray();
        this.values = () => valuesAndStrs;
    }


    protected override void Rebind() {
        base.Rebind();
        NodeHTML.Q<Label>("Key").text = Description();
        NodeHTML.Q<Label>("Value").text = values()[Index].key;
    }

    private void ScaleEndpoint(VisualElement ep) {
        ep.ScaleTo(1.35f, 0.06f, Easers.EOutSine)
            .Then(() => ep.ScaleTo(1f, 0.15f))
            .Run(Controller);
    }
    protected override UIResult Left() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index - 1);
            OnChange(v[index].val);
            if (UseDefaultAnimations)
                ScaleEndpoint(NodeHTML.Q("Left"));
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values();
        if (v.Length > 0) {
            index = M.Mod(v.Length, index + 1);
            OnChange(v[index].val);
            if (UseDefaultAnimations)
                ScaleEndpoint(NodeHTML.Q("Right"));
        }
        return new UIResult.StayOnNode();
    }
}


public class ComplexLROptionUINode<T> : BaseLROptionUINode<T>, IComplexOptionNodeLR {
    private readonly VisualTreeAsset objectTree;
    private readonly Action<T, VisualElement, bool> binder;
    private readonly Func<T[]> values;
    public int Index {
        get => index = M.Clamp(0, values().Length - 1, index);
        set => index = value;
    }
    
    public T Value => values()[Index];

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
            var ve = objectTree.CloneTreeWithoutContainer();
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

//TODO: i eventually need to think of a better way to handle text input
public class TextInputNode : UINode {
    public string DataWIP { get; private set; } = "";
    private bool isEntryEnabled = false;
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => isEntryEnabled ? DataWIP.Insert(bdCursorIdx, "|") : DataWIP;

    public TextInputNode(LString title) : base(title) { }

    protected override void Rebind() {
        string t = Description();
        NodeHTML.Q<Label>("Prefix").text = string.IsNullOrEmpty(t) ? "" : t + ":";
        NodeHTML.Q("FadedBack").style.display = DisplayWIP.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        NodeHTML.Q<Label>("Label").text = DisplayWIP;
    }

    private static readonly string[] validChars = 
        "abcdefghijklmnopqrstuvwxyz0123456789".Select(x => x.ToString()).ToArray();

    private static string KCToString(KeyCode kc) {
        if (kc is >= KeyCode.A and <= KeyCode.Z)
            return ((char)('a' + (kc - KeyCode.A))).ToString();
        if (kc is >= KeyCode.Alpha0 and <= KeyCode.Alpha9)
            return ((char)('0' + (kc - KeyCode.Alpha0))).ToString();
        throw new Exception($"No handling for keycode {kc}");
    }
    
    public override UIResult? CustomEventHandling() {
        if (!isEntryEnabled) return null;
        if (InputManager.GetKeyTrigger(KeyCode.Space).Active) {
            DataWIP = DataWIP.Insert(bdCursorIdx, " ");
            ++cursorIdx;
            return new UIResult.StayOnNode();
        }
        foreach (var kc in InputManager.Alphanumeric)
            if (InputManager.GetKeyTrigger(kc).Active) {
                DataWIP = DataWIP.Insert(bdCursorIdx, (InputManager.GetKeyTrigger(KeyCode.LeftShift).Active || 
                                                       InputManager.GetKeyTrigger(KeyCode.RightShift).Active) ? 
                    KCToString(kc).ToUpper() : KCToString(kc).ToLower());
                ++cursorIdx;
                return new UIResult.StayOnNode();
            }
        if (InputManager.GetKeyTrigger(KeyCode.Backspace).Active) {
            DataWIP =
                ((cursorIdx > 1) ? DataWIP[..(cursorIdx - 1)] : "") +
                ((cursorIdx < DataWIP.Length) ? DataWIP[cursorIdx..] : "");
            cursorIdx = Math.Max(0, cursorIdx - 1);
            return new UIResult.StayOnNode();
        } else if (InputManager.GetKeyTrigger(KeyCode.Return).Active || InputManager.GetKeyTrigger(KeyCode.Escape).Active) {
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
        UICommand.Confirm => !isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req),
        UICommand.Back => isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req),
        _ => base.NavigateInternal(req)
    };

    public override void Leave(bool animate) {
        isEntryEnabled = false;
        base.Leave(animate);
    }
}

public class UIButton : UINode {
    public enum ButtonType {
        Cancel,
        Confirm,
        Danger
    }

    private bool isConfirm = false;
    public ButtonType Type { get; }
    private readonly Func<UIButton, UIResult> onClick;
    private readonly bool requiresConfirm;

    public UIButton(Func<LString> descriptor, ButtonType type, Func<UIButton, UIResult> onClick) : base(descriptor) {
        this.Type = type;
        With(type switch {
            ButtonType.Confirm => "confirm",
            ButtonType.Danger => "danger",
            _ => null
        });
        requiresConfirm = type == ButtonType.Danger;
        this.onClick = onClick;
        UseDefaultAnimations = false;
    }

    public UIButton(LString descriptor, ButtonType type, Func<UIButton, UIResult> onClick) : 
        this(() => descriptor, type, onClick) { }

    public static UIButton Cancel(UINode source) =>
        new(() => LocalizedStrings.Generic.generic_cancel, ButtonType.Cancel, _ => 
            new UIResult.ReturnToTargetGroupCaller(source.Group));
    
    public static UIButton Back(UINode source) =>
        new(() => LocalizedStrings.Generic.generic_back, ButtonType.Cancel, _ => 
            new UIResult.ReturnToTargetGroupCaller(source.Group));

    public static UIButton Delete(Func<bool> deleter, Func<UIResult> returner) =>
        new(() => LocalizedStrings.Generic.generic_delete, ButtonType.Danger, 
            _ => deleter() ? returner() : new UIResult.StayOnNode(true));
    
    public static UIButton Save(Func<bool> saver, UIResult returner) =>
        new(() => LocalizedStrings.Generic.generic_save, ButtonType.Confirm, 
            _ => saver() ? returner : new UIResult.StayOnNode(true));
    public static UIButton Load(Func<bool> load, UIResult returner, bool danger=false) =>
        new(() => LocalizedStrings.Generic.generic_load, danger ? ButtonType.Danger : ButtonType.Confirm, 
            _ => load() ? returner : new UIResult.StayOnNode(true));
    
    protected override void Rebind() {
        Label!.text = isConfirm ? LocalizedStrings.UI.are_you_sure : Description();
    }

    protected override UIResult NavigateInternal(UICommand req) {
        if (req == UICommand.Confirm) {
            if (requiresConfirm) {
                // ReSharper disable once AssignmentInConditionalExpression
                return (isConfirm = !isConfirm) ? new UIResult.StayOnNode(false) : onClick(this);
            } else
                return onClick(this);
        }
        return base.NavigateInternal(req);
    }

    protected override IEnumerable<Func<ICancellee, Task>> EnterAnimations() {
        yield return cT => 
            NodeHTML.transform.ScaleTo(1.16f, 0.1f, Easers.EOutSine, cT)
                .Then(() => NodeHTML.transform.ScaleTo(1.1f, 0.1f, cT: cT))
                .Run(Controller, new CoroutineOptions(true));
    }
    protected override IEnumerable<Func<ICancellee, Task>> LeaveAnimations() {
        yield return cT =>
            NodeHTML.transform.ScaleTo(1f, 0.1f, Easers.EOutSine, cT)
                .Run(Controller, new CoroutineOptions(true));
    }
    public override void Leave(bool animate) {
        isConfirm = false;
        base.Leave(animate);
    }
}

}