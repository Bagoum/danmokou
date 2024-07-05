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
    /// The node is in the same group as the Focused node.
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
    public LString DescriptionOrEmpty => RootView.VM.Description ?? LString.Empty;
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
    /// Get the CSS world rect of this object's center. Note that this is in pixels with the top left as (0, 0).
    /// </summary>
    public Rect WorldLocation => HTML.worldBound;
    public IStyle Style => HTML.style;
    
    /// <summary>
    /// The VisualElement constructed or referenced by this node.
    /// </summary>
    public VisualElement HTML { get; private set; } = null!;
    /// <summary>
    /// Whether or not the node's HTML has been built yet.
    /// </summary>
    public bool Built { get; private set; }
    
    /// <summary>
    /// #Body: the interactable area of the node.
    /// </summary>
    public VisualElement? BodyHTML => HTML.Q("Body");
    public VisualElement BodyOrNodeHTML => BodyHTML ?? HTML;

    /// <summary>
    /// Parent of HTML. Either Render.HTML or a descendant of Render.HTML.
    /// </summary>
    private VisualElement ContainerHTML { get; set; } = null!;

    /// <summary>
    /// True iff the node is visible, regardless of whether the group is visible.
    /// <br/>True by default, overridable by <see cref="IUIViewModel"/>.<see cref="IUIViewModel.ShouldBeVisible"/>.
    /// </summary>
    public bool IsNodeVisible {
        get {
            for (int ii = 0; ii < Views.Count; ++ii)
                if (!Views[ii].ViewModel.ShouldBeVisible(this))
                    return false;
            return true;
        }
    }
        
    
    /// <summary>
    /// True iff the node is visible (as determined by <see cref="VisibleIf"/>) and the group is also visible.
    /// </summary>
    public bool IsVisible => Group.Visible && IsNodeVisible;
    
    /// <summary>
    /// True iff the node is enabled.
    /// <br/>True by default, overridable by <see cref="IUIViewModel"/>.<see cref="IUIViewModel.ShouldBeEnabled"/>.
    /// </summary>
    public bool IsEnabled {
        get {
            for (int ii = 0; ii < Views.Count; ++ii)
                if (!Views[ii].ViewModel.ShouldBeEnabled(this))
                    return false;
            return true;
        }
    }
    
    public bool AllowInteraction => (Passthrough != true) && Group.Interactable && IsNodeVisible;
    public int IndexInGroup => Group.Nodes.IndexOf(this);

    /// <summary>
    /// Move this node to a specified index in the parent group's container, and also move its HTML likewise.
    /// </summary>
    public UINode MoveToIndex(int index) {
        Group.Nodes.Remove(this);
        Group.Nodes.Insert(index, this);
        HTML.MoveToIndex(index);
        return this;
    }
    public bool Destroyed { get; private set; } = false;
    
    #region InitOptions

    /// <summary>
    /// Set whether or not this node should be "skipped over" for navigation events.
    /// <br/>Defaults to null, which operates like False, but can be overriden by the group.
    /// <br/>If the node is not visible (due to VisibleIf returning false), then it will also be skipped.
    /// <br/>Note: use <see cref="UpdatePassthrough"/> for runtime changes.
    /// </summary>
    public bool? Passthrough { get; set; } = null;

    public void UpdatePassthrough(bool? b) {
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
    
    /// <inheritdoc cref="IUIViewModel.ShouldBeVisible"/>
    public Func<bool>? VisibleIf {
        set => RootView.VM.VisibleIf = value;
    }
    
    /// <inheritdoc cref="IUIViewModel.ShouldBeEnabled"/>
    public Func<bool>? EnabledIf {
        set => RootView.VM.EnabledIf = value;
    }
    /// <summary>
    /// Given the HTML of the RenderSpace, select the object under which to construct this node's HTML.
    /// <br/>If not overriden, uses h => h.
    /// </summary>
    public Func<VisualElement, VisualElement>? BuildTarget { get; set; }

    /// <summary>
    /// Overrides the visualTreeAsset used to construct this node's HTML.
    /// <br/>This overrides <see cref="IUIView"/>.<see cref="IUIView.Prefab"/>.
    /// </summary>
    public VisualTreeAsset? Prefab { get; init; }

    /// <inheritdoc cref="UIView.Builder"/>
    public Func<VisualElement, VisualElement>? Builder { get; init; }

    /// <summary>
    /// View rendering configurations to bind to this node's HTML.
    /// </summary>
    private List<IUIView> Views { get; } = new();
    public RootNodeView RootView { get; }
    
    /// <summary>
    /// Called after the HTML is built.
    /// </summary>
    public Action<UINode>? OnBuilt { get; set; }
    private string?[]? cssClasses;
    
    private UIGroup? currentTooltip = null;
    
    /// <summary>
    /// Overrides <see cref="Navigate"/> for Confirm entries.
    /// </summary>
    public Func<UINode, ICursorState, UIResult?>? OnConfirm { get; init; }
    
    private readonly UIGroup? _showHideGroup;
    /// <summary>
    /// A UIGroup to show on entry and hide on exit.
    /// </summary>
    public UIGroup? ShowHideGroup {
        get => _showHideGroup;
        init {
            if (value != null) {
                if (value.Visibility is not GroupVisibilityControl.UpdateOnLeaveHide)
                    value.Visibility = new GroupVisibilityControl.UpdateOnLeaveHide(value);
                value.Parent = Group;
            }
            _showHideGroup = value;
        } 
    }
    
    #endregion

    public UINode WithCSS(params string?[] clss) {
        cssClasses = clss;
        if (Built)
            AddCSSClasses();
        return this;
    }
    
    public UIRenderSpace Render => Group.Render;
    public UIScreen Screen => Group.Screen;
    public UIController Controller => Group.Controller;
    /// <summary>
    /// Creates a ReturnToTargetGroupCaller targeting this node's group.
    /// <br/>Use this from a popup to return navigation to this node.
    /// </summary>
    public UIResult ReturnToGroup => new UIResult.ReturnToTargetGroupCaller(this);

    public UINode(LString? description) {
        Views.Add(RootView = new RootNodeView(this, description));
    }
    
    public UINode() : this(null as LString) { }

    public UINode(params IUIView[] views) : this(null as LString) {
        Views.AddRange(views);
    }

    #region Construction
    
    /// <summary>
    /// Bind a <see cref="IUIView"/> that configures dynamic rendering for some aspect of the node.
    /// </summary>
    public UINode Bind<T>(T view) where T : IUIView {
        Views.Add(view);
        if (Built) {
            view.Bind(HTML);
            view.OnBuilt(this);
        }
        return this;
    }

    /// <inheritdoc cref="Bind{T}(T)"/>
    public UINode Bind<T>(Func<UINode, T> view) where T : IUIView => Bind(view(this));

    /// <inheritdoc cref="Bind{T}(T)"/>
    public UINode Bind(params IUIView[] views) {
        foreach (var v in views)
            Bind<IUIView>(v);
        return this;
    }

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

    public static Func<UIRenderSpace, UIColumn> SimpleTTGroup(UINode node) =>
        rs => new UIColumn(rs, node);
    public static Func<UIRenderSpace, UIColumn> SimpleTTGroup(LString text) =>
        rs => new UIColumn(rs, SimpleTTNode(text));
    
    public static UINode SimpleTTNode(LString text) =>
        new UINode(text) { Prefab = XMLUtils.Prefabs.PureTextNode }.WithCSS(XMLUtils.highVisClass);
    

    protected virtual void RegisterEvents() {
        bool isInElement = false;
        bool startedClickHere = false;
        //It's more mouse-friendly to use BodyHTML when possible so empty space on rows doesn't draw events
        var evtBinder = BodyOrNodeHTML;
        evtBinder.RegisterCallback<PointerEnterEvent>(evt => {
        #if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            //PointerEnter is still issued while there's no touch, at the last touched point
            if (evt.pressure <= 0)
                return;
        #endif
            //don't fire pointer enter events if the renderer is animating out
            //NB: UIController doesn't allow this command to cross screens, so any persistent screens are show-only
            if (AllowInteraction && Render.ShouldBeVisibleInTree) {
                Controller.QueueInput(new UIPointerCommand.Goto(this));
                evt.StopPropagation();
            }
            isInElement = true;
        });
        evtBinder.RegisterCallback<PointerLeaveEvent>(evt => {
            //Logs.Log($"Leave {Description()}");
            //For freeform groups ONLY, moving the cursor off a node should deselect it.
            if (AllowInteraction && Group is UIFreeformGroup && Controller.Current == this)
                Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Back, this) { Silent = true });
            isInElement = false;
            startedClickHere = false;
        });
        evtBinder.RegisterCallback<PointerDownEvent>(evt => {
            //Logs.Log($"Down {Description()}");
            if (AllowInteraction)
                foreach (var view in Views)
                    view.OnMouseDown(this, evt);
            startedClickHere = true;
        });
        evtBinder.RegisterCallback<PointerUpEvent>(evt => {
            //Logs.Log($"Click {Description()}");
            //button 0, 1, 2 = left, right, middle click
            //Right click is handled as UIBack in InputManager. UIBack is global (it does not depend
            // on the current UINode), but click-to-confirm is done via callbacks specific to the UINode.
            if (AllowInteraction && evt.button == 0) {
                //This event will not actually do anything unless the current node is this or null;
                // see UIPointerCommand.ValidForCurrent
                if (isInElement && startedClickHere)
                    Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Confirm, this));
                foreach (var view in Views)
                    view.OnMouseUp(this, evt);
                evt.StopPropagation();
            }
            startedClickHere = false;
        });
    }

    private void AddCSSClasses() {
        if (cssClasses is null) return;
        foreach (var cls in cssClasses)
            if (!string.IsNullOrWhiteSpace(cls))
                HTML.AddToClassList(cls);
    }
    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        ContainerHTML = BuildTarget?.Invoke(Render.HTML) ?? Render.HTML;
        Func<VisualElement, VisualElement>? builder = null;
        VisualTreeAsset? prefab = null;
        foreach (var view in Views) {
            if (view.Builder != null) {
                if (builder != null)
                    throw new Exception("Multiple builders defined for node");
                builder = view.Builder;
            }
            if (view.Prefab != null) {
                if (prefab != null)
                    throw new Exception("Multiple view prefabs defined for node");
                prefab = view.Prefab;
            }
        }
        if ((builder ??= Builder) != null) {
            HTML = builder!(ContainerHTML);
        } else {
            prefab = Prefab != null ? Prefab : 
                prefab != null ? prefab : map.SearchByType(this, true);
            HTML = prefab.CloneTreeNoContainer();
            ContainerHTML.Add(HTML);
        }
        AddCSSClasses();
        foreach (var view in Views)
            view.Bind(HTML);
        Built = true;
        RegisterEvents();
        foreach (var view in Views)
            view.OnBuilt(this);
        OnBuilt?.Invoke(this);
        AddToken(ServiceLocator.Find<IDMKLocaleProvider>().TextLocale.OnChange.Subscribe(_ => {
            foreach (var view in Views)
                view.ReprocessForLanguageChange();
        }));
    }

    private List<IDisposable> Tokens { get; } = new();
    public UINode AddToken(IDisposable token) {
        Tokens.Add(token);
        return this;
    }

    /// <summary>
    /// Destroy this node when `obj` is destroyed.
    /// </summary>
    public void BindLifetime(IModelObject obj) => AddToken(obj.WhenDestroyed(this.Remove));

    public void MarkDestroyed() {
        if (Destroyed) return;
        Destroyed = true;
        foreach (var view in Views)
            view.OnDestroyed(this);
        Views.Clear();
        HTML.ClearBindings();
        Tokens.DisposeAll();
    }

    public void Remove() {
        CloseDependencies(false);
        MarkDestroyed();
        Group.Nodes.Remove(this);
        HTML.RemoveFromHierarchy();
        ShowHideGroup?.Destroy();
        Controller.MoveCursorAwayFromNode(this);
    }

    #endregion

    #region Drawing
    
    public void ScrollTo() {
        HTML.Focus();
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

    //call when group.interactable is turned off, which should modify selection
    public void ReprocessSelection() => UpdateSelection(Selection);
    
    #endregion
    
    #region Navigation

    /// <inheritdoc cref="ICursorState.CustomEventHandling"/>
    public UIResult? CustomEventHandling() {
        foreach (var view in Views)
            if (view.ViewModel.CustomEventHandling(this) is { } res)
                return res;
        return null;
    }

    /// <summary>
    /// Provided an input, modify the state of the UI appropriately, and return instructions for
    ///  control flow modification.
    /// </summary>
    public UIResult Navigate(UICommand req, ICursorState cs) {
        if (req == UICommand.Confirm) {
            if (!IsEnabled)
                return new UIResult.StayOnNode(true);
            if (OnConfirm?.Invoke(this, cs) is { } cres)
                return cres;
            for (int ii = 0; ii < Views.Count; ++ii)
                if (Views[ii].ViewModel.OnConfirm(this, cs) is { } vmres)
                    return vmres;
        }
        if (req == UICommand.ContextMenu) {
            for (int ii = 0; ii < Views.Count; ++ii)
                if (Views[ii].ViewModel.OnContextMenu(this, cs) is { } vmres)
                    return vmres;
            return UIGroup.NoOp;
        }
        return NavigateInternal(req, cs);
    }

    protected virtual UIResult NavigateInternal(UICommand req, ICursorState cs) {
        for (int ii = 0; ii < Views.Count; ++ii)
            if (Views[ii].ViewModel.Navigate(this, cs, req) is { } vmres)
                return vmres;
        return Group.Navigate(this, req);
    }

    public void Enter(bool animate, ICursorState cs) {
        if (CacheOnEnter) Controller.TentativeCache(this);
        _ = ShowHideGroup?.Visibility.OnEnterGroup();
        RemakeTooltip(cs);
        foreach (var view in Views)
            view.OnEnter(this, cs, animate);
    }

    public virtual void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {   
        if (!isEnteringPopup)
            CloseDependencies(animate);
        foreach (var view in Views)
            view.OnLeave(this, cs, animate, isEnteringPopup);
    }

    /// <summary>
    /// Called when the navigation is moved to a descendant of this node (and was not previously so).
    /// Call order: RemovedFromNavHierarchy > AddedToNavHierarchy > Leave > Enter
    /// </summary>
    public void AddedToNavHierarchy() {
        foreach (var view in Views)
            view.OnAddedToNavHierarchy(this);
    }
    
    /// <summary>
    /// Called when the group stack is moved outside a descendant of this node (and was not previously so).
    /// Call order: RemovedFromNavHierarchy > AddedToNavHierarchy > Leave > Enter
    /// </summary>
    public void RemovedFromNavHierarchy() {
        CloseDependencies(true);
        foreach (var view in Views)
            view.OnRemovedFromNavHierarchy(this);
    }

    private void CloseDependencies(bool animate) {
        _ = ShowHideGroup?.Visibility.OnLeaveGroup();
        CloseTooltip(animate);
    }

    public void CloseTooltip(bool animate) {
        if (currentTooltip is null) return;
        var ctt = currentTooltip;
        currentTooltip = null;
        if (animate) {
            _ = ctt.LeaveGroup().ContinueWithSync();
        } else
            ctt.Destroy();
    }
    public void RemakeTooltip(ICursorState cs) {
        var prevExists = currentTooltip is not null;
        foreach (var view in Views)
            if (view.ViewModel.Tooltip(this, cs, prevExists) is {} vtt) {
                SetTooltip(vtt);
                return;
            }
    }

    public void SetTooltip(UIGroup? tooltip) {
        CloseTooltip(tooltip is null || tooltip.Render.IsAnimating);
        currentTooltip = tooltip;
    }

    #endregion
}

public class EmptyNode : UINode {
    public IFixedXMLObject? Source => MaybeView<FixedXMLView>()?.VM.Descr;

    public EmptyNode(params IUIView[] views) : base(views) {
        RootView.DisableAnimations();
    }

    public ICObservable<float> CreateCenterOffsetChildX(ICObservable<float> childX) =>
        Source!.CreateCenterOffsetChildX(childX);

    public ICObservable<float> CreateCenterOffsetChildY(ICObservable<float> childY) =>
        Source!.CreateCenterOffsetChildY(childY);
    
    public static EmptyNode MakeUnselector(Func<UINode, ICursorState, UICommand, UIResult?>? unselectNav) =>
        new(new FixedXMLView(new(new UnselectorFixedXML()) { 
                Navigator = (n, cs, req) => {
                    if (unselectNav?.Invoke(n, cs, req) is { } res)
                        return res;
                    return req == UICommand.Confirm ? UIGroup.SilentNoOp : null;
                }
            }) { AsEmpty = true });
}

public class PassthroughNode : UINode {
    public PassthroughNode(LString? desc = null) : base(desc) {
        Passthrough = true;
    }
}
public class TwoLabelUINode : UINode {
    public TwoLabelUINode(LString description1, Func<string> description2, IObservable<Unit>? updater) : base(description1) {
        var view = new SimpleLabelView(description2, "Label2");
        Bind(view);
        if (updater != null)
            AddToken(view.DirtyOn(updater));
    }
    public TwoLabelUINode(LString description1, ILabelViewModel vm) : base(description1) {
        Bind(new BaseLabelView<ILabelViewModel>(vm, "Label2"));
    }

    public TwoLabelUINode(LString description1, object description2) : base(description1) {
        OnBuilt = OnBuilt.Then(n => n.HTML.Q<Label>("Label2").text = description2.ToString());
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
    public TransferNode(LString? description, UINode target) : 
        base(description, () => new UIResult.GoToNode(target)) { }
    public TransferNode(LString? description, UIGroup target) : 
        base(description, () => new UIResult.GoToNode(target)) { }
    public TransferNode(LString? description, UIScreen target) : 
        base(description, () => new UIResult.GoToNode(target.Groups[0])) { }
}

public class ConfirmFuncNode : UINode {
    private bool isConfirm = false;
    public Func<ConfirmFuncNode, UIResult> Command { get; }

    public ConfirmFuncNode(LString description, Func<ConfirmFuncNode, UIResult> command) : base() {
        this.Command = command;
        Bind(new FlagView(new(() => isConfirm, LocalizedStrings.UI.are_you_sure, description)));
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

public interface IBaseLROptionNode {
    int Index { get; set; }
}
//Separated for buildMap compatibilty
public interface ILROptionNode : IBaseLROptionNode {
}
public interface IComplexLROptionNode : IBaseLROptionNode {
}
public abstract class BaseLROptionNode<T> : UINode, IDerivativeViewModel {
    private readonly ITwoWayBinder<T> binder;
    public IUIViewModel Delegator => binder.ViewModel;
    public T Value {
        get => binder.Value;
        protected set {
            if (!EqualityComparer<T>.Default.Equals(binder.Value, value))
                binder.Value = value;
        }
    }

    public BaseLROptionNode(LString Description, ITwoWayBinder<T> binder) : base(Description) {
        this.binder = binder;
    }

    protected override void RegisterEvents() {
        base.RegisterEvents();
        HTML.Q("Left").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Left, this));
            evt.StopPropagation();
        });
        HTML.Q("Right").RegisterCallback<PointerUpEvent>(evt => {
            Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Right, this));
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

public class LROptionNode<T> : BaseLROptionNode<T>, ILROptionNode {
    private class View : UIView<LROptionNode<T>>, IUIView {
        public View(LROptionNode<T> data) : base(data) { }
        public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
            HTML.Q<Label>("Key").text = node.DescriptionOrEmpty;
        }

        protected override BindingResult Update(in BindingContext context) {
            HTML.Q<Label>("Value").text = ViewModel.lastKey;
            return base.Update(in context);
        }
    }

    private readonly Func<(LString key, T val)[]> values;
    private int _index;
    public int Index {
        get => _index;
        set => Update(value, values()[value]);
    }
    private LString lastKey { get; set; } = null!;

    private void Update(int index, (LString key, T val) selected) {
        _index = index;
        lastKey = selected.key;
        Value = selected.val;
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

    public void OnModelUpdated() => SetIndexFromVal(Value);
    
    public LROptionNode(LString description, ITwoWayBinder<T> binder, Func<(LString, T)[]> values) : base(description, binder) {
        this.values = values;
        Bind(new View(this));
        AddToken(binder.ViewModel.EvModelUpdated.Subscribe(_ => OnModelUpdated()));
        SetIndexFromVal(binder.Value);
    }
    public LROptionNode(LString description, ITwoWayBinder<T> binder, (LString, T)[] values) : 
        this(description, binder, () => values) { }

    public LROptionNode(LString description, Evented<T> ev, (LString, T)[] values) :
        this(description, new EventedBinder<T>(ev, null), () => values) { }

    private void ScaleEndpoint(VisualElement ep) {
        Controller.PlayAnimation(
            ep.ScaleTo(1.35f, 0.06f, Easers.EOutSine)
            .Then(() => ep.ScaleTo(1f, 0.15f)));
    }
    protected override UIResult Left() {
        var v = values();
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind - 1);
            Update(ind, v[ind]);
            ScaleEndpoint(HTML.Q("Left"));
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values();
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind + 1);
            Update(ind, v[ind]);
            ScaleEndpoint(HTML.Q("Right"));
        }
        return new UIResult.StayOnNode();
    }
}

public class ComplexLROptionNode<T> : BaseLROptionNode<T>, IComplexLROptionNode {
    private class View : UIView<ComplexLROptionNode<T>> {
        public View(ComplexLROptionNode<T> data) : base(data) { }
        protected override BindingResult Update(in BindingContext context) {
            VM.HTML.Q<Label>("Key").text = VM.DescriptionOrEmpty;
            var container = VM.HTML.Q("LR2ChildContainer");
            container.Clear();
            foreach (var (i, v) in VM.values.Enumerate()) {
                VM.HTML.Q("LR2ChildContainer").Add(VM.realizer(i, v, i == VM.Index));
            }
            return base.Update(in context);
        }
    }
    
    private readonly Func<int, T, bool, VisualElement> realizer;
    private readonly T[] values;
    private int _index;
    public int Index {
        get => _index;
        set => Update(value, values[value]);
    }

    private void Update(int index, T selected) {
        _index = index;
        Value = selected;
    }
    
    public void SetIndexFromVal(T val) {
        var vals = values;
        var ind = 0;
        for (int ii = 0; ii < vals.Length; ++ii)
            if (EqualityComparer<T>.Default.Equals(vals[ii], val)) {
                ind = ii;
                break;
            }
        Update(ind, vals[ind]);
    }

    public ComplexLROptionNode(LString description, ITwoWayBinder<T> binder, T[] values, Func<int, T, bool, VisualElement> realizer) : 
        base(description, binder) {
        this.values = values;
        this.realizer = realizer;
        Bind(new View(this));
        AddToken(binder.ViewModel.EvModelUpdated.Subscribe(_ => SetIndexFromVal(Value)));
        SetIndexFromVal(binder.Value);
    }

    protected override UIResult Left() {
        var v = values;
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind - 1);
            Update(ind, v[ind]);
        }
        return new UIResult.StayOnNode();
    }
    protected override UIResult Right() {
        var v = values;
        var ind = BMath.Clamp(0, v.Length - 1, Index);
        if (v.Length > 0) {
            ind = BMath.Mod(v.Length, ind + 1);
            Update(ind, v[ind]);
        }
        return new UIResult.StayOnNode();
    }
}

public class KeyRebindInputNode : UINode, IUIViewModel {
    private readonly LString? title;
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideViewHash { get; set; }
    public long GetViewHash() => 0;
    private readonly KeyRebindInputNodeView view;
    private class KeyRebindInputNodeView : UIView<KeyRebindInputNode> {
        public KeyRebindInputNodeView(KeyRebindInputNode data) : base(data) {
            UpdateTrigger = BindingUpdateTrigger.WhenDirty;
        }
        protected override BindingResult Update(in BindingContext context) {
            var n = ViewModel;
            string t = n.title ?? "";
            n.HTML.Q<Label>("Prefix").text = string.IsNullOrEmpty(t) ? "" : t + ":";
            n.HTML.Q("FadedBack").style.display = !n.isEntryEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            n.HTML.Q<Label>("Label").text = n.isEntryEnabled ?
                n.lastHeld == null ?
                    "Press desired keys" :
                    string.Join("+", n.lastHeld.Select(l => l.Description)) :
                "\t";
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

    public KeyRebindInputNode(LString title, Action<IInspectableInputBinding[]?> applier, Mode mode) : base() {
        this.title = title;
        this.applier = applier;
        this.mode = mode;
        WithCSS(fontControlsClass);
        Bind(view = new(this));
    }

    UIResult? IUIViewModel.CustomEventHandling(UINode _) {
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
    private readonly LString? title;
    public BindingUpdateTrigger UpdateTrigger { get; set; }
    public Func<long>? OverrideViewHash { get; set; }
    public long GetViewHash() => (isEntryEnabled, DataWIP, bdCursorIdx).GetHashCode();
    private class TextInputNodeView : UIView<TextInputNode> {
        public TextInputNodeView(TextInputNode data) : base(data) { }
        protected override BindingResult Update(in BindingContext context) {
            var n = ViewModel;
            string t = n.title ?? "";
            n.HTML.Q<Label>("Prefix").text = string.IsNullOrEmpty(t) ? "" : t + ":";
            var disp = n.DisplayWIP;
            n.HTML.Q("FadedBack").style.display = disp.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            n.HTML.Q<Label>("Label").text = disp.Length == 0 ? "\t" : n.DisplayWIP;
            return base.Update(in context);
        }
    }
    public string DataWIP { get; private set; } = "";
    private bool isEntryEnabled = false;
    private int cursorIdx = 0;
    private int bdCursorIdx => Math.Min(cursorIdx, DataWIP.Length);
    private string DisplayWIP => isEntryEnabled ? DataWIP.Insert(bdCursorIdx, "|") : DataWIP;


    public TextInputNode(LString title) : base(title) {
        this.title = title;
        Bind(new TextInputNodeView(this));
    }

    UIResult? IUIViewModel.CustomEventHandling(UINode _) {
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
    private readonly Func<UIButton, UIResult>? onClick;
    private readonly bool requiresConfirm;

    public UIButton(LString? descriptor, ButtonType type, Func<UIButton, UIResult>? onClick = null) : base(descriptor) {
        this.Type = type;
        RootView.EnterAnimation = ButtonEnterAnimation;
        RootView.LeaveAnimation = ButtonLeaveAnimation;
        WithCSS(type switch {
            ButtonType.Confirm => "confirm",
            ButtonType.Danger => "danger",
            _ => null
        });
        requiresConfirm = type == ButtonType.Danger;
        this.onClick = onClick;
        if (descriptor != null)
            Bind(new FlagView(new(() => isConfirm, LocalizedStrings.UI.are_you_sure, descriptor)));
    }

    public static Func<T, UIResult> GoBackCommand<T>(UINode source) => _ =>
        source.ReturnToGroup;

    public static Func<UIButton, UIResult> GoBackCommand(UINode source) => GoBackCommand<UIButton>(source);
    public static UIButton Cancel(UINode source) =>
        new(LocalizedStrings.Generic.generic_cancel, ButtonType.Cancel, GoBackCommand(source));
    
    public static UIButton Back(UINode source, LString? description = null) =>
        new(description ?? LocalizedStrings.Generic.generic_back, ButtonType.Cancel, GoBackCommand(source));

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
            if (requiresConfirm && !isConfirm) {
                isConfirm = true;
                return new UIResult.StayOnNode(false);
            } else {
                isConfirm = false;
                return onClick?.Invoke(this) ?? base.NavigateInternal(req, cs);
            }
        }
        return base.NavigateInternal(req, cs);
    }
    
    public override void Leave(bool animate, ICursorState cs, bool isEnteringPopup) {
        isConfirm = false;
        base.Leave(animate, cs, isEnteringPopup);
    }

    public static readonly Func<UINode, ICancellee, Task?> ButtonEnterAnimation = (n, cT) => 
        n.Controller.PlayAnimation(
            n.HTML.transform.ScaleTo(1.16f, 0.1f, Easers.EOutSine, cT)
                .Then(() => n.HTML.transform.ScaleTo(1.1f, 0.1f, cT: cT)));
    
    public static readonly Func<UINode, ICancellee, Task?> ButtonLeaveAnimation = (n, cT) =>
        n.Controller.PlayAnimation(n.HTML.transform.ScaleTo(1f, 0.1f, Easers.EOutSine, cT));

}

}