using System;
using System.Collections;
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
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
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
public enum UINodeSelection {
    /// <summary>
    /// The cursor is on the node.
    /// </summary>
    Focused = 4,
    
    /// <summary>
    /// This node is the source of the currently active popup.
    /// </summary>
    PopupSource = 3,
    
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
    public LString? Description { get; }
    public LString DescriptionOrEmpty => Description ?? LString.Empty;
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
    
    /// <summary>
    /// Get the CSS world rect of this object's center. Note that this is in pixels with the top left as (0, 0).
    /// </summary>
    public Rect WorldLocation => HTML.worldBound;
    public IStyle Style => HTML.style;
    /// <summary>
    /// The .node VisualElement constructed by this node. Usually points to the only child of HTML, which should have the class .node.
    /// </summary>
    public VisualElement NodeHTML { get; private set; } = null!;
    /// <summary>
    /// Whether or not the node's HTML has been built yet.
    /// </summary>
    public bool Built { get; private set; }
    public VisualElement? BodyHTML => NodeHTML.Q("Body");
    public VisualElement BodyOrNodeHTML => BodyHTML ?? NodeHTML;

    /// <summary>
    /// Parent of HTML. Either Render.HTML or a descendant of Render.HTML.
    /// </summary>
    private VisualElement ContainerHTML { get; set; } = null!;

    /// <summary>
    /// True iff the node is visible, regardless of whether the group is visible.
    /// </summary>
    public bool IsNodeVisible => (VisibleIf?.Invoke() ?? true);
    
    /// <summary>
    /// True iff the node is visible (as determined by <see cref="VisibleIf"/>) and the group is also visible.
    /// </summary>
    public bool IsVisible => Group.Visible && IsNodeVisible;
    
    /// <summary>
    /// True iff the node is enabled (true by default, overridable by <see cref="EnabledIf"/>).
    /// </summary>
    public bool IsEnabled => EnabledIf?.Invoke() ?? true;
    
    public bool AllowInteraction => (Passthrough != true) && Group.Interactable && IsNodeVisible;
    public int IndexInGroup => Group.Nodes.IndexOf(this);
    public bool Destroyed { get; private set; } = false;

    /// <summary>
    /// If this node is positioned absolute (in the CSS sense), this property
    ///  contains information on node sizing/positioning.
    /// </summary>
    public IFixedXMLObject? AbsoluteLocationSource { get; private set; } = null;

    public void ConfigureAbsoluteLocation(IFixedXMLObject source, Vector2? pivot = null, Action<UINode>? extraOnBuild = null, bool useVisiblityPassthrough = true) {
        if (AbsoluteLocationSource != null)
            throw new Exception($"Duplicate defintion of {nameof(AbsoluteLocationSource)}");
        this.AbsoluteLocationSource = source;
        //UseDefaultAnimations = false;
        Navigator = source.Navigate;
        var existingOnBuild = OnBuilt;
        OnBuilt = n => {
            _ = source.IsVisible.Subscribe(b => {
                if (useVisiblityPassthrough)
                    UpdatePassthrough(!b);
                //Allows opacity fade-out
                n.HTML.pickingMode = b ? PickingMode.Position : PickingMode.Ignore;
                n.HTML.style.opacity = b ? 1 : 0;
            });

            n.HTML.ConfigureAbsolute(pivot).ConfigureFixedXMLPositions(source);
            
            //n.HTML.style.backgroundColor = new Color(0, 1, 0, 0.4f);
            existingOnBuild?.Invoke(this);
            extraOnBuild?.Invoke(this);
        };
    }
    
    #region InitOptions

    /// <summary>
    /// Set whether or not this node should be "skipped over" for navigation events.
    /// <br/>Defaults to null, which operates like False, but can be overriden by the group.
    /// <br/>If the node is not visible (due to VisibleIf returning false), then it will also be skipped.
    /// <br/>Note: use <see cref="UpdatePassthrough"/> for runtime changes.
    /// </summary>
    public bool? Passthrough { get; set; } = null;

    public void UpdatePassthrough(bool? b) {
        //if ((b != true) && (Passthrough == true))
        //    MONKEYPATCH_mouseDelay = 0.5f;
        if (Passthrough == b) return;
        Passthrough = b;
        if (b is true)
            Controller.MoveCursorAwayFromNode(this);
    }
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
    /// Overrides the visualTreeAsset used to construct this node's HTML.
    /// <br/>This overrides <see cref="IUIView"/>.<see cref="IUIView.Prefab"/>.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }

    /// <summary>
    /// View rendering configurations to bind to this node's HTML.
    /// </summary>
    private List<IUIView> Views { get; } = new();
    public RootNodeView RootView { get; }
    
    /// <summary>
    /// Called after the HTML is built.
    /// </summary>
    public Action<UINode>? OnBuilt { get; set; }
    
    /// <summary>
    /// Called when this node gains focus.
    /// </summary>
    public Action<UINode, ICursorState>? OnEnter { get; set; }
    
    /// <summary>
    /// Called along with <see cref="OnEnter"/> when this node gains focus,
    ///  or when <see cref="RemakeTooltip"/> is called.
    /// <br/>Creates a tooltip to the upper-right of this node.
    /// </summary>
    public Func<UINode, ICursorState, bool, UIGroup?>? CreateTooltip { get; set; }
    private UIGroup? currentTooltip = null;
    
    /// <summary>
    /// Called when this node loses focus.
    /// </summary>
    public Action<UINode, ICursorState>? OnLeave { get; init; }
    
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
    public Func<UINode, UICommand, UIResult?>? Navigator { get; set; }
    /// <summary>
    /// Overrides Navigate for Confirm entries. Runs after Navigator but before Navigate.
    /// </summary>
    public Func<UINode, ICursorState, UIResult?>? OnConfirm { get; set; }
    
    /// <summary>
    /// Provides a menu to show when the "context menu" button (C by default) is pressed while this node is active.
    /// </summary>
    public Func<UINode, ICursorState, UIResult?>? OnContextMenu { get; set; }
    
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
    /// Animation played when focus is placed on the node. Defaults to <see cref="DefaultEnterAnimation"/>.
    /// </summary>
    public Maybe<Func<UINode, ICancellee, Task>?> EnterAnimation { get; set; } = 
        Maybe<Func<UINode, ICancellee, Task>?>.None;
    
    /// <summary>
    /// Animation played when focus is removed from the node. Defaults to <see cref="DefaultLeaveAnimation"/>.
    /// </summary>
    public Maybe<Func<UINode, ICancellee, Task>?> LeaveAnimation { get; set; } = 
        Maybe<Func<UINode, ICancellee, Task>?>.None;

    public UINode DisableAnimations() {
        EnterAnimation = LeaveAnimation = null;
        return this;
    }

    public Task? PlayAnimation(Func<UINode, ICancellee, Task>? anim) {
        if (anim != null) {
            animToken.SoftCancel();
            animToken = new();
            return anim(this, animToken);
        }
        return null;
    }
    
    
    #endregion
    
    private Cancellable animToken = new();

    public UINode WithCSS(params string?[] clss) {
        void AddClassesToNode(UINode n) {
            foreach (var cls in clss)
                if (!string.IsNullOrWhiteSpace(cls))
                    n.NodeHTML.AddToClassList(cls);
        }
        if (Built)
            AddClassesToNode(this);
        else
            OnBuilt = ((Action<UINode>)AddClassesToNode).Then(OnBuilt);
        return this;
    }
    
    public UIRenderSpace Render => Group.Render;
    public UIScreen Screen => Group.Screen;
    public UIController Controller => Group.Controller;
    /// <summary>
    /// Creates a ReturnToTargetGroupCaller targeting this node's group.
    /// </summary>
    public UIResult ReturnGroup => new UIResult.ReturnToTargetGroupCaller(this);

    public UINode(LString? description) {
        this.Description = description;
        Views.Add(RootView = new RootNodeView(this));
    }
    
    public UINode() : this(null) { }

    #region Construction
    
    /// <summary>
    /// Add a <see cref="IUIView"/> that configures dynamic rendering for some aspect of the node.
    /// </summary>
    public UINode WithView<T>(T view) where T : IUIView {
        Views.Add(view);
        if (Built) {
            view.Bind(HTML);
            view.NodeBuilt(this);
        }
        return this;
    }

    /// <inheritdoc cref="WithView{T}(T)"/>
    public UINode WithView<T>(Func<UINode, T> view) where T : IUIView => WithView(view(this));

    public UINode WithRootView(Action<RootNodeView> updater) {
        updater(this.RootView);
        return this;
    }

    /// <summary>
    /// Retrieve the view of type T, or throw an exception.
    /// </summary>
    public T View<T>() where T : class, IUIView => MaybeView<T>() ??
                                            throw new Exception($"No view found for type {typeof(T).RName()}");
    
    /// <summary>
    /// Retrieve the view of type T.
    /// </summary>
    public T? MaybeView<T>() where T: class, IUIView {
        for (int ii = 0; ii < Views.Count; ++ii)
            if (Views[ii] is T view)
                return view;
        return null;
    }

    /// <summary>
    /// Configure a tooltip that will appear to the upper-right of this node when this node is focused.
    /// <br/>Tooltips cannot be interacted with.
    /// </summary>
    public UINode PrepareTooltip(Func<UINode, ICursorState, UINode?> element) {
        CreateTooltip = (n, cs, animate) => 
            element(n, cs) is {} node ?
                this.MakeTooltip(SimpleTTGroup(node), animateEntry: animate)
                : null;
        return this;
    }

    public Func<UIRenderSpace, UIColumn> SimpleTTGroup(UINode node) =>
        rs => new UIColumn(rs, node);
    public Func<UIRenderSpace, UIColumn> SimpleTTGroup(LString text) =>
        rs => new UIColumn(rs, SimpleTTNode(text));
    
    public UINode SimpleTTNode(LString text) =>
        new UINode(text) { Prefab = XMLUtils.Prefabs.PureTextNode }.WithCSS(XMLUtils.highVisClass);


    /// <inheritdoc cref="PrepareTooltip(System.Func{Danmokou.UI.XML.UINode,Danmokou.UI.XML.ICursorState,Danmokou.UI.XML.UINode?})"/>
    public UINode PrepareTooltip(Func<LString?> text) =>
        PrepareTooltip((_, _) => text() is { } txt ? SimpleTTNode(txt) : null);
    
    /// <inheritdoc cref="PrepareTooltip(System.Func{Danmokou.UI.XML.UINode,Danmokou.UI.XML.ICursorState,Danmokou.UI.XML.UINode?})"/>
    public UINode PrepareTooltip(LString text) =>
        PrepareTooltip(() => text);
    

    /// <summary>
    /// Configure a context menu that appears to the lower-right of this node when C is pressed while this node is focused.
    /// <br/>A context menu is like a popup. While the options menu is active, nothing else receives interaction.
    /// </summary>
    public UINode PrepareContextMenu(Func<UINode, ICursorState, UINode[]?> options) {
        OnContextMenu = (n, cs) => {
            if (options(n, cs) is not { } opts)
                return null;
            return PopupUIGroup.CreateContextMenu(n, opts);
        };
        return this;
    }

    protected virtual void RegisterEvents() {
        bool isInElement = false;
        bool startedClickHere = false;
        //It's a bit more mouse-friendly to use BodyHTML when possible so empty space on rows doesn't draw events
        var evtBinder = BodyOrNodeHTML;
        evtBinder.RegisterCallback<PointerEnterEvent>(evt => {
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            //PointerEnter is still issued while there's no touch, at the last touched point
            if (evt.pressure <= 0)
                return;
        #endif
            if (AllowInteraction) {
                Controller.QueueEvent(new UIPointerCommand.Goto(this));
                evt.StopPropagation();
            }
            isInElement = true;
        });
        evtBinder.RegisterCallback<PointerLeaveEvent>(evt => {
            //Logs.Log($"Leave {Description()}");
            //For freeform groups ONLY, moving the cursor off a node should deselect it.
            if (AllowInteraction && Group is UIFreeformGroup && Controller.Current == this)
                Controller.QueueEvent(new UIPointerCommand.NormalCommand(UICommand.Back, this) { Silent = true });
            isInElement = false;
            startedClickHere = false;
        });
        evtBinder.RegisterCallback<PointerDownEvent>(evt => {
            //Logs.Log($"Down {Description()}");
            if (AllowInteraction)
                OnMouseDown?.Invoke(this, evt);
            startedClickHere = true;
        });
        evtBinder.RegisterCallback<PointerUpEvent>(evt => {
            //Logs.Log($"Click {Description()}");
            //button 0, 1, 2 = left, right, middle click
            //Right click is handled as UIBack in InputManager. UIBack is global (it does not depend
            // on the current UINode), but click-to-confirm is done via callbacks specific to the UINode.
            if (AllowInteraction && evt.button == 0) {
                if (isInElement && startedClickHere)
                    Controller.QueueEvent(new UIPointerCommand.NormalCommand(UICommand.Confirm, this));
                OnMouseUp?.Invoke(this, evt);
                evt.StopPropagation();
            }
            startedClickHere = false;
        });
    }
    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        var prefab = Prefab;
        if (prefab == null) {
            foreach (var view in Views) {
                if (view.Prefab != null) {
                    if (prefab != null)
                        throw new Exception("Multiple view prefabs defined for node");
                    prefab = view.Prefab;
                }
            }
        }
        if (prefab == null)
            prefab = map.SearchByType(this, true);
        NodeHTML = prefab.CloneTreeNoContainer();
        if (Description != null) {
            var label = NodeHTML.Q<Label>();
            if (label != null)
                label.text = Description;
        }
        (ContainerHTML = BuildTarget?.Invoke(Render.HTML) ?? Render.HTML).Add(HTML);
        foreach (var view in Views)
            view.Bind(HTML);
        Built = true;
        RegisterEvents();
        foreach (var view in Views)
            view.NodeBuilt(this);
        OnBuilt?.Invoke(this);
    }

    public void MarkDestroyed() {
        Destroyed = true;
        foreach (var view in Views)
            view.NodeDestroyed(this);
    }

    public void Remove() {
        CloseDependencies(false);
        MarkDestroyed();
        Group.Nodes.Remove(this);
        HTML.RemoveFromHierarchy();
        Controller.MoveCursorAwayFromNode(this);
    }

    #endregion

    #region Drawing
    
    public void ScrollTo() {
        NodeHTML.Focus();
        if (ContainerHTML is ScrollView sv)
            sv.ScrollTo(HTML);
        else {
            for (var g = ContainerHTML; g != null; g = g.parent) {
                if (g.parent is ScrollView sv_) {
                    sv_.ScrollTo(g);
                    return;
                }
            }
        }
    }

    private static UINodeSelection Max(UINodeSelection a, UINodeSelection b) => a > b ? a : b;

    public UINodeSelection Selection { get; private set; } = UINodeSelection.Default;

    public void UpdateSelection(UINodeSelection selection) {
        //For inaccessible groups, we make them group-focused
        //Use Interactable instead of HasInteractableNodes in order to avoid encompassing groups that are
        // dynamically inacessible
        if (selection >= UINodeSelection.Default && !Group.Interactable)
            selection = Max(selection, UINodeSelection.GroupFocused);
        Selection = selection;
        //TODO not sure where to put this
        if (currentTooltip is not null)
            HTML.SetTooltipAbsolutePosition(currentTooltip.Render.HTML);
    }
    
    #endregion
    
    #region Navigation

    /// <summary>
    /// Custom navigation handling that takes priority over all UI navigation except queued events.
    /// </summary>
    public virtual UIResult? CustomEventHandling() => null;

    /// <summary>
    /// Provided an input, modify the state of the UI appropriately, and return instructions for
    ///  control flow modification.
    /// </summary>
    public UIResult Navigate(UICommand req, ICursorState cs) {
        if (req == UICommand.Confirm && !IsEnabled) return new UIResult.StayOnNode(true);
        return Navigator?.Invoke(this, req) ?? req switch {
            UICommand.Confirm when OnConfirm != null => OnConfirm(this, cs) ?? NavigateInternal(req, cs),
            UICommand.ContextMenu => OnContextMenu?.Invoke(this, cs) ?? UIGroup.NoOp,
            _ => NavigateInternal(req, cs)
        };
    }

    protected virtual UIResult NavigateInternal(UICommand req, ICursorState cs) => Group.Navigate(this, req);
    
    public void Enter(bool animate, ICursorState cs) {
        if (CacheOnEnter) Controller.TentativeCache(this);
        if (animate) {
            _ = PlayAnimation(EnterAnimation.Valid ? EnterAnimation.Value : DefaultEnterAnimation());
        }
        ShowHideGroup?.EnterShow();
        RemakeTooltip(cs);
        OnEnter?.Invoke(this, cs);
        Group.EnteredNode(this, animate);
    }

    public virtual void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {   
        if (animate) {
            _ = PlayAnimation(LeaveAnimation.Valid ? LeaveAnimation.Value : DefaultLeaveAnimation());
        }
        if (!isEnteringPopup)
            CloseDependencies(animate);
        OnLeave?.Invoke(this, cs);
    }

    public void RemovedFromGroupStack() {
        CloseDependencies(true);
    }

    private void CloseDependencies(bool animate) {
        //if (ShowHideGroup != null)
            //Logs.Log($"Closing show/hide on {DescriptionOrEmpty} ({Group})");
        ShowHideGroup?.LeaveHide();
        CloseTooltip(animate);
    }

    public void CloseTooltip(bool animate) {
        if (currentTooltip is null) return;
        var ctt = currentTooltip;
        currentTooltip = null;
        if (animate) {
            _ = ctt.LeaveHide().ContinueWithSync(Finish);
        } else
            Finish();
        void Finish() {
            ctt.Destroy();
            (ctt.Render as UIRenderConstructed)?.Destroy();
        }
    }
    public void RemakeTooltip(ICursorState cs) {
        SetTooltip(CreateTooltip?.Invoke(this, cs, currentTooltip is null));
    }

    public void SetTooltip(UIGroup? tooltip) {
        CloseTooltip(tooltip is null || tooltip.Render.IsAnimating);
        currentTooltip = tooltip;
    }
    
    protected virtual Func<UINode, ICancellee, Task>? DefaultEnterAnimation() => (n, cT) => 
        n.NodeHTML.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
                                .Then(() => n.NodeHTML.transform.ScaleTo(1f, 0.13f, cT: cT))
                                .Run(Controller, UIController.AnimOptions);

    protected virtual Func<UINode, ICancellee, Task>? DefaultLeaveAnimation() => null;

    #endregion
}

public class EmptyNode : UINode {
    public IFixedXMLObject? Source { get; }

    public EmptyNode() {
        DisableAnimations();
    }
    public EmptyNode(IFixedXMLObject source, Action<EmptyNode>? onBuild = null, bool useVisiblityPassthrough = true) : 
            base(source.Descriptor) {
        this.Source = source;
        DisableAnimations().ConfigureAbsoluteLocation(source, extraOnBuild: n => {
            n.HTML.ConfigureEmpty();
            onBuild?.Invoke(this);
        }, useVisiblityPassthrough: useVisiblityPassthrough);
    }

    public ICObservable<float> CreateCenterOffsetChildX(ICObservable<float> childX) =>
        Source!.CreateCenterOffsetChildX(childX);

    public ICObservable<float> CreateCenterOffsetChildY(ICObservable<float> childY) =>
        Source!.CreateCenterOffsetChildY(childY);
}

public class PassthroughNode : UINode {
    public PassthroughNode(LString? desc = null) : base(desc) {
        Passthrough = true;
    }
}
public class TwoLabelUINode : UINode {
    public TwoLabelUINode(LString description1, Func<LString> description2, IObservable<Unit>? updater) : base(description1) {
        var view = new LabelView<LString>(new(description2, x => x.Value), "Label2");
        if (updater != null)
            view.DirtyOn(updater);
        WithView(view);
    }
    public TwoLabelUINode(LString description1, Func<string> description2, IObservable<Unit>? updater) : base(description1) {
        var view = new LabelView(description2, "Label2");
        if (updater != null)
            view.DirtyOn(updater);
        WithView(view);
    }
    public TwoLabelUINode(LString description1, ILabelViewModel vm) : base(description1) {
        WithView(new LabelView(vm, "Label2"));
    }

    public TwoLabelUINode(LString description1, object description2) : base(description1) {
        OnBuilt = OnBuilt.Then(n => n.NodeHTML.Q<Label>("Label2").text = description2.ToString());
    }
}
public class FuncNode : UINode {
    public Func<FuncNode, ICursorState, UIResult> Command { get; }

    public FuncNode(LString? description, Func<FuncNode, ICursorState, UIResult> command) : base(description) {
        this.Command = command;
    }
    public FuncNode(LString? description, Func<FuncNode, UIResult> command) : this(description, (n,cs) => command(n)) { }
    public FuncNode(LString? description, Func<UIResult> command) : this(description, (_, _) => command()) { }
    public FuncNode(LString? description, Action command) : this(description, (_, _) => {
        command();
        return new UIResult.StayOnNode();
    }) { }
    public FuncNode(LString? description, Func<bool> command) : this(description, (_, _) => new UIResult.StayOnNode(!command())) { }
    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) {
        if (req == UICommand.Confirm)
            return Command(this, cs);
        return base.NavigateInternal(req, cs);
    }
}

public class OpenUrlNode : FuncNode {
    public OpenUrlNode(LString? Description, string URL) : base(Description, () => {
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

    public ConfirmFuncNode(LString description, Func<ConfirmFuncNode, UIResult> command) : base(description) {
        this.Command = command;
        if (Description != null)
            WithView(new FlagLabelView(new(() => isConfirm, LocalizedStrings.UI.are_you_sure, Description)));
    }
    public ConfirmFuncNode(LString description, Func<UIResult> command) : this(description, _ => command()) { }
    public ConfirmFuncNode(LString description, Action command) : this(description, () => {
        command();
        return new UIResult.StayOnNode();
    }) { }
    public ConfirmFuncNode(LString description, Func<bool> command) : this(description, 
        () => new UIResult.StayOnNode(!command())) { }

    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) {
        if (req == UICommand.Confirm)
            // ReSharper disable once AssignmentInConditionalExpression
            return (isConfirm = !isConfirm) ? new UIResult.StayOnNode(false) : Command(this);
        return base.NavigateInternal(req, cs);
    }

    public override void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {
        isConfirm = false;
        base.Leave(animate, cs, isEnteringPopup);
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
    public Action<T> OnChange { get; }

    public BaseLROptionUINode(LString Description, Action<T> OnChange) : base(Description) {
        this.OnChange = OnChange;
    }

    protected override void RegisterEvents() {
        base.RegisterEvents();
        NodeHTML.Q("Left").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueueEvent(new UIPointerCommand.NormalCommand(UICommand.Left, this));
            evt.StopPropagation();
        });
        NodeHTML.Q("Right").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueueEvent(new UIPointerCommand.NormalCommand(UICommand.Right, this));
            evt.StopPropagation();
        });
    }

    protected abstract UIResult Left();
    protected abstract UIResult Right();

    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) => req switch {
        UICommand.Left => Left(),
        UICommand.Right => Right(),
        _ => base.NavigateInternal(req, cs)
    };
}

public class OptionNodeLR<T> : BaseLROptionUINode<T>, IOptionNodeLR, IDerivativeViewModel {
    private readonly ITwoWayBinder<T> binder;
    public IUIViewModel Delegator => binder.ViewModel;
    
    private class View : UIView<OptionNodeLR<T>> {
        public View(OptionNodeLR<T> data) : base(data) { }
        public override void NodeBuilt(UINode node) {
            base.NodeBuilt(node);
            Node.NodeHTML.Q<Label>("Key").text = Node.DescriptionOrEmpty;
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.NodeHTML.Q<Label>("Value").text = ViewModel.lastKey;
            return base.Update(in context);
        }
    }

    private readonly Func<(LString key, T val)[]> values;
    private int _index;
    public int Index {
        get => _index;
        set => Update(value, values()[value]);
    }
    private LString lastKey { get; set; }
    public T Value => binder.Value;

    private void Update(int index, (LString key, T val) selected) {
        _index = index;
        lastKey = selected.key;
        if (!EqualityComparer<T>.Default.Equals(binder.Value, selected.val))
            binder.Value = selected.val;
    }
    

    public void SetIndexFromVal(T val) {
        var vals = values();
        var ind = 0;
        for (int ii = 0; ii < vals.Length; ++ii)
            if (EqualityComparer<T>.Default.Equals(vals[ii].val, val)) {
                ind = ii;
                break;
            }
        Update(ind, vals[ind]);
    }
    
    public OptionNodeLR(LString description, ITwoWayBinder<T> binder, Func<(LString, T)[]> values) : base(description, null!) {
        this.values = values;
        this.binder = binder;
        var view = new View(this);
        WithView(view);
        (view as ITokenized).AddToken(binder.ValueUpdatedFromModel.Subscribe(_ => SetIndexFromVal(Value)));
        SetIndexFromVal(binder.Value);
    }
    public OptionNodeLR(LString description, ITwoWayBinder<T> binder, (LString, T)[] values) : 
        this(description, binder, () => values) { }

    public OptionNodeLR(LString description, Evented<T> ev, (LString, T)[] values) :
        this(description, new EventedBinder<T>(ev, null), () => values) { }

    private void ScaleEndpoint(VisualElement ep) {
        ep.ScaleTo(1.35f, 0.06f, Easers.EOutSine)
            .Then(() => ep.ScaleTo(1f, 0.15f))
            .Run(Controller, UIController.AnimOptions);
    }
    protected override UIResult Left() {
        var v = values();
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind - 1);
            Update(ind, v[ind]);
            ScaleEndpoint(NodeHTML.Q("Left"));
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values();
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind + 1);
            Update(ind, v[ind]);
            ScaleEndpoint(NodeHTML.Q("Right"));
        }
        return new UIResult.StayOnNode();
    }
}


public class ComplexLROptionUINode<T> : BaseLROptionUINode<T>, IComplexOptionNodeLR, IUIViewModel {
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideHashHandler { get; set; }
    public long GetViewHash() => Index.GetHashCode();
    private class ComplexLROptionNodeView : UIView<ComplexLROptionUINode<T>> {
        public ComplexLROptionNodeView(ComplexLROptionUINode<T> data) : base(data) { }
        protected override BindingResult Update(in BindingContext context) {
            ViewModel.NodeHTML.Q<Label>("Key").text = ViewModel.DescriptionOrEmpty;
            ViewModel.NodeHTML.Q("LR2ChildContainer").Clear();
            foreach (var (i, v) in ViewModel.values.Enumerate()) {
                var ve = ViewModel.objectTree.CloneTreeNoContainer();
                ViewModel.NodeHTML.Q("LR2ChildContainer").Add(ve);
                ViewModel.binder(v, ve, i == ViewModel.index);
            }
            return base.Update(in context);
        }
    }
    
    private readonly VisualTreeAsset objectTree;
    private readonly Action<T, VisualElement, bool> binder;
    private readonly T[] values;
    private int index;
    public int Index {
        get => index = M.Clamp(0, values.Length - 1, index);
        set => index = value;
    }
    
    public T Value => values[Index];

    public ComplexLROptionUINode(LString description, VisualTreeAsset objectTree, Action<T> onChange, T[] values, Action<T, VisualElement, bool> binder) : 
        base(description, onChange) {
        this.values = values;
        this.objectTree = objectTree;
        this.binder = binder;
        this.index = 0;
        WithView(new ComplexLROptionNodeView(this));
    }

    protected override UIResult Left() {
        var v = values;
        if (v.Length > 0) {
            index = BMath.Mod(v.Length, index - 1);
            OnChange(v[index]);
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values;
        if (v.Length > 0) {
            index = BMath.Mod(v.Length, index + 1);
            OnChange(v[index]);
        }
        return new UIResult.StayOnNode();
    }
}

public class KeyRebindInputNode : UINode, IUIViewModel {

    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideHashHandler { get; set; }
    public long GetViewHash() => 0;
    private readonly KeyRebindInputNodeView view;
    private class KeyRebindInputNodeView : UIView<KeyRebindInputNode> {
        public KeyRebindInputNodeView(KeyRebindInputNode data) : base(data) {
            UpdateTrigger = BindingUpdateTrigger.WhenDirty;
        }
        protected override BindingResult Update(in BindingContext context) {
            var n = ViewModel;
            string t = n.DescriptionOrEmpty;
            n.NodeHTML.Q<Label>("Prefix").text = string.IsNullOrEmpty(t) ? "" : t + ":";
            n.NodeHTML.Q("FadedBack").style.display = !n.isEntryEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            n.NodeHTML.Q<Label>("Label").text = n.isEntryEnabled ?
                n.lastHeld == null ?
                    "Press desired keys" :
                    string.Join("+", n.lastHeld.Select(l => l.Description)) :
                "";
            return base.Update(in context);
        }
    }
    public enum Mode {
        KBM,
        Controller
    }

    private readonly Mode mode;
    private readonly Action<IInspectableInputBinding[]?> applier;
    private bool isHoldReady = false;
    private IInspectableInputBinding[]? lastHeld = null;
    private bool isEntryEnabled = false;

    public KeyRebindInputNode(LString title, Action<IInspectableInputBinding[]?> applier, Mode mode) : base(title) {
        this.applier = applier;
        this.mode = mode;
        WithCSS(fontControlsClass);
        WithView(view = new(this));
    }

    public override UIResult? CustomEventHandling() {
        if (!isEntryEnabled) return null;
        var heldKeys = mode switch {
            Mode.Controller => InputManager.CurrentlyHeldRebindableControllerKeys,
            _ => InputManager.CurrentlyHeldRebindableKeys
        };
        if (heldKeys == null && lastHeld != null) {
            //We're done
            applier(lastHeld);
            return EnableDisableEntry();
        }

        //Require that the user first hold no keys before pressing any keys
        // This avoids issues with accidentally parsing the Z on entry
        if (!isHoldReady) {
            if (heldKeys == null)
                isHoldReady = true;
            return new UIResult.StayOnNode();
        }

            //Maintain the largest set of held keys.
        //Eg. If I hold shift+ctrl+R, and then release R, it should continue to display shift+ctrl+R.
        if (lastHeld == null || heldKeys?.Length >= lastHeld.Length) {
            lastHeld = heldKeys;
            view.MarkDirty();
        }

        if (InputManager.GetKeyTrigger(KeyCode.Escape).Active)
            return EnableDisableEntry();

        return new UIResult.StayOnNode();
    }
    
    
    private UIResult EnableDisableEntry() {
        isEntryEnabled = !isEntryEnabled;
        isHoldReady = false;
        lastHeld = null;
        view.MarkDirty();
        return new UIResult.StayOnNode();
    }
    
    //This is only reached if custom handling does nothing
    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) => req switch {
        UICommand.Confirm => !isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req, cs),
        UICommand.Back => isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req, cs),
        _ => base.NavigateInternal(req, cs)
    };

    public override void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {
        isEntryEnabled = false;
        base.Leave(animate, cs, isEnteringPopup);
    }
}

public class TextInputNode : UINode, IUIViewModel {
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideHashHandler { get; set; }
    public long GetViewHash() => (isEntryEnabled, DataWIP, bdCursorIdx).GetHashCode();
    private class TextInputNodeView : UIView<TextInputNode> {
        public TextInputNodeView(TextInputNode data) : base(data) { }
        protected override BindingResult Update(in BindingContext context) {
            var n = ViewModel;
            string t = n.DescriptionOrEmpty;
            n.NodeHTML.Q<Label>("Prefix").text = string.IsNullOrEmpty(t) ? "" : t + ":";
            n.NodeHTML.Q("FadedBack").style.display = n.DisplayWIP.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            n.NodeHTML.Q<Label>("Label").text = n.DisplayWIP;
            return base.Update(in context);
        }
    }
    public string DataWIP { get; private set; } = "";
    private bool isEntryEnabled = false;
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => isEntryEnabled ? DataWIP.Insert(bdCursorIdx, "|") : DataWIP;


    public TextInputNode(LString title) : base(title) {
        WithView(new TextInputNodeView(this));
    }

    public override UIResult? CustomEventHandling() {
        if (!isEntryEnabled) return null;
        if (InputManager.TextInput is {} c) {
            DataWIP = DataWIP.Insert(bdCursorIdx, c.ToString());
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
    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) => req switch {
        UICommand.Left => Left(),
        UICommand.Right => Right(),
        UICommand.Confirm => !isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req, cs),
        UICommand.Back => isEntryEnabled ? EnableDisableEntry() : base.NavigateInternal(req, cs),
        _ => base.NavigateInternal(req, cs)
    };

    public override void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {
        isEntryEnabled = false;
        base.Leave(animate, cs, isEnteringPopup);
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

    public UIButton(LString? descriptor, ButtonType type, Func<UIButton, UIResult> onClick) : base(descriptor) {
        this.Type = type;
        WithCSS(type switch {
            ButtonType.Confirm => "confirm",
            ButtonType.Danger => "danger",
            _ => null
        });
        requiresConfirm = type == ButtonType.Danger;
        this.onClick = onClick;
        if (descriptor != null)
            WithView(new FlagLabelView(new(() => isConfirm, LocalizedStrings.UI.are_you_sure, descriptor)));
    }

    public static Func<T, UIResult> GoBackCommand<T>(UINode source) => _ =>
        new UIResult.ReturnToTargetGroupCaller(source);

    public static Func<UIButton, UIResult> GoBackCommand(UINode source) => GoBackCommand<UIButton>(source);
    public static UIButton Cancel(UINode source) =>
        new(LocalizedStrings.Generic.generic_cancel, ButtonType.Cancel, GoBackCommand(source));
    
    public static UIButton Back(UINode source) =>
        new(LocalizedStrings.Generic.generic_back, ButtonType.Cancel, GoBackCommand(source));

    public static UIButton Delete(Func<bool> deleter, Func<UIResult> returner) =>
        new(LocalizedStrings.Generic.generic_delete, ButtonType.Danger, 
            _ => deleter() ? returner() : new UIResult.StayOnNode(true));
    
    public static UIButton Save(Func<bool> saver, UIResult returner) =>
        new(LocalizedStrings.Generic.generic_save, ButtonType.Confirm, 
            _ => saver() ? returner : new UIResult.StayOnNode(true));
    public static UIButton Load(Func<bool> load, UIResult returner, bool danger=false) =>
        new(LocalizedStrings.Generic.generic_load, danger ? ButtonType.Danger : ButtonType.Confirm, 
            _ => load() ? returner : new UIResult.StayOnNode(true));

    protected override UIResult NavigateInternal(UICommand req, ICursorState cs) {
        if (req == UICommand.Confirm) {
            if (requiresConfirm) {
                // ReSharper disable once AssignmentInConditionalExpression
                return (isConfirm = !isConfirm) ? new UIResult.StayOnNode(false) : onClick(this);
            } else
                return onClick(this);
        }
        return base.NavigateInternal(req, cs);
    }

    protected override Func<UINode, ICancellee, Task> DefaultEnterAnimation() => (n, cT) => 
            n.NodeHTML.transform.ScaleTo(1.16f, 0.1f, Easers.EOutSine, cT)
                .Then(() => n.NodeHTML.transform.ScaleTo(1.1f, 0.1f, cT: cT))
                .Run(Controller, UIController.AnimOptions);
    
    protected override Func<UINode, ICancellee, Task> DefaultLeaveAnimation() => (n, cT) =>
            n.NodeHTML.transform.ScaleTo(1f, 0.1f, Easers.EOutSine, cT)
                .Run(Controller, UIController.AnimOptions);
    
    public override void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {
        isConfirm = false;
        base.Leave(animate, cs, isEnteringPopup);
    }
}

}