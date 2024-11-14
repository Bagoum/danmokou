using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;
using UnityEngine.UIElements;
using Helpers = SuzunoyaUnity.Helpers;

namespace Danmokou.UI.XML {
public abstract class UIRenderSpace {
    protected List<UIGroup> Sources { get; } = new();
    protected List<IUIView> Views { get; } = new();
    private VisualElement? _html = null;
    protected abstract VisualElement _FirstLoadHTML();
    public VisualElement HTML {
        get {
            if (_html is null) {
                _html = _FirstLoadHTML() ?? throw new Exception("Couldn't load HTML");
                LocalUpdateVisibility(true);
                foreach (var v in Views)
                    v.Bind(Screen.Controller.MVVM, _html);
                foreach (var v in Views)
                    v.OnBuilt(this);
            }
            return _html;
        }
    }
    public UIScreen Screen { get; }
    public UIRenderSpace? Parent { get; }
    public LazyEvented<bool> IsHTMLVisible { get; }
    protected virtual bool BaseShouldBeLocalVisible => !VisibleWhenSourcesVisible || HasVisibleSource;
    private bool ShouldBeLocalVisible => ShouldBeLocalVisibleOverride ?? BaseShouldBeLocalVisible;
    public bool ShouldBeTreeVisible => ShouldBeLocalVisible && (Parent?.ShouldBeTreeVisible ?? true);
    
    public bool? ShouldBeLocalVisibleOverride { get; private set; }
    
    /// <summary>
    /// True if this render space is allowed to modify the HTML visibility. If false, will not run any
    ///  updates to HTML when the value of <see cref="IsHTMLVisible"/> changes.
    /// </summary>
    protected virtual bool ControlsHTML => true;

    /// <summary>
    /// List of (descendant) render spaces who may run animations when this render's visibility changes.
    /// </summary>
    private List<UIRenderSpace> VisibilityDependents { get; } = new();

    /// <summary>
    /// By default, render spaces will not animate out if a parent space is already rendering out.
    /// <br/>If this is set to true, then such animations will be allowed.
    /// <br/>False by default, except if this is a child of <see cref="UIRenderAbsoluteTerritory"/>.
    /// </summary>
    public bool AnimateOutWithParent { get; set; }

    private bool _useTreeForHTMLVisibility = false;

    /// <summary>
    /// By default, the visibility of a render space's HTML is set and animated independently of its parent,
    ///  though any ancestor HTML being invisible will also make any descendant HTML invisible.
    /// <br/>When this is set, the local HTML visibility will only be set to true if all ancestors are also visible.
    /// <br/>This is only really useful for animations, since in some cases we want to play animations
    ///  on child render groups that are otherwise always visible based on when the parent's visibility changes.
    /// <br/>This also sets <see cref="AnimateOutWithParent"/> to true.
    /// </summary>
    public UIRenderSpace UseTreeVisible() {
        _useTreeForHTMLVisibility = true;
        AnimateOutWithParent = true;
        for (var p = Parent; p != null; p = p.Parent)
            p.VisibilityDependents.Add(this);
        return this;
    }
    
    /// <summary>
    /// If set to true, then the the HTML for a render group will be disabled unless at least one group
    ///  that renders to this renderspace (or to a child renderspace) is visible.
    /// </summary>
    public bool VisibleWhenSourcesVisible { get; set; } = false;
    public bool HasVisibleSource {
        get {
            for (var ii = 0; ii < Sources.Count; ii++)
                if (Sources[ii].Visible)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Returns true if all sources in this renderer are descendants of `group`.
    /// <br/>Note: returns false if there are no sources in this renderer.
    /// </summary>
    public bool AllSourcesDescendFrom(UIGroup group) {
        if (Sources.Count == 0)
            return false;
        var gh = group.Hierarchy;
        foreach (var s in Sources)
            if (!s.Hierarchy.IsWeakPrefix(gh))
                return false;
        return true;
    }
    public UIController Controller => Screen.Controller;
    
    /// <summary>
    /// Animation played when the render space becomes visible or invisible.
    /// (The animation to the initial visibility state is skipped.)
    /// </summary>
    public InOutAnimCfg? AnimCfg { get; set; }
    
    public bool IsFirstRender { get; set; } = true;
    public virtual bool IsAnimating => animateToken?.Cancelled is false;
    public bool IsAnimatingInTree => IsAnimating || Parent?.IsAnimatingInTree is true;
    protected Cancellable? animateToken;

    public UIRenderSpace(UIScreen screen, UIRenderSpace? parent) {
        this.Screen = screen;
        this.Parent = parent;
        AnimateOutWithParent = parent is UIRenderAbsoluteTerritory;
        IsHTMLVisible = new(() => _useTreeForHTMLVisibility ? ShouldBeTreeVisible : ShouldBeLocalVisible);
        Screen.Renderers.Add(this);
    }

    public Task OverrideLocalVisibility(bool? overrid, bool fast = false) {
        ShouldBeLocalVisibleOverride = overrid;
        return LocalUpdateVisibility(fast);
    }

    private List<Task> UpdateVisibilityDependents() {
        var tasks = new List<Task>();
        foreach (var dep in VisibilityDependents)
            if (dep.LocalUpdateVisibility() is {IsCompletedSuccessfully:false} t)
                tasks.Add(t);
        return tasks;
    }
    private Task? AnimateIn(bool skip, InOutAnimCfg? anim) {
        HTML.style.display = DisplayStyle.Flex;
        if (!skip && VisibilityDependents.Count > 0) {
            var cT = Cancellable.Replace(ref animateToken);
            var tasks = UpdateVisibilityDependents();
            if (anim?.In is not null)
                tasks.Add(anim.In(this, cT));
            //cancel cT so IsAnimating becomes false
            return tasks!.All().ContinueWithSync(cT.Cancel);
        } else if (anim?.In is not null) {
            var cT = Cancellable.Replace(ref animateToken);
            if (skip) 
                cT.SoftCancel();
            return anim.In(this, cT).ContinueWithSync(cT.Cancel);
        } else
            return null;
            
    }

    private Task? AnimateOut(bool skip, InOutAnimCfg? anim) {
        //don't do anything if a parent renderer is disappearing
        if (!skip && !AnimateOutWithParent && Parent?.ShouldBeTreeVisible is false)
            return null;
        skip |= anim?.IsEffectivelyHidden(this) is true;
        if (!skip && VisibilityDependents.Count > 0) {
            HTML.style.display = DisplayStyle.Flex;
            var tasks = UpdateVisibilityDependents();
            var cT = Cancellable.Replace(ref animateToken);
            if (anim?.Out is not null)
                tasks.Add(anim.Out(this, cT));
            return tasks!.All().ContinueWithSync(() => {
                if (!cT.IsHardCancelled())
                    HTML.style.display = DisplayStyle.None;
                cT.Cancel();
            });
        } else if (anim?.Out is not null) {
            var cT = Cancellable.Replace(ref animateToken);
            if (skip) 
                cT.SoftCancel();
            else 
                HTML.style.display = DisplayStyle.Flex;
            return anim.Out(this, cT).ContinueWithSync(() => {
                if (!cT.IsHardCancelled())
                    HTML.style.display = DisplayStyle.None;
                cT.Cancel();
            });
        } else {
            HTML.style.display = DisplayStyle.None;
            return null;
        }
    }

    //visibility changes due to group dependencies must be cascaded upwards
    public Task UpdateVisibility(bool fast = false) {
        return (Parent?.UpdateVisibility(fast)).And(LocalUpdateVisibility(fast));
    }

    protected Task LocalUpdateVisibility(bool fast = false) {
        var oldVis = IsHTMLVisible.Value;
        IsHTMLVisible.Recompute();
        var newVis = IsHTMLVisible.Value;
        Task? t = null;
        if (_html != null && ControlsHTML) {
            var ifr = IsFirstRender;
            IsFirstRender = false;
            var skipAnim = ifr || fast || Screen.State.Value is UIScreenState.Inactive;
            if (newVis && (ifr || !oldVis))
                t = AnimateIn(skipAnim, AnimCfg);
            else if (!newVis && (ifr || oldVis))
                t = AnimateOut(skipAnim, AnimCfg);
        }
        //AnimateIn/Out will cancel previous animations. These cancellations are not failures
        // and should not cause consumers (specifically UIController transition management) to error
        return t?.DontReportCancellation() ?? Task.CompletedTask;
    }
    
    /// <summary>
    /// Add an <see cref="IUIView"/> directly to the HTML for this render space.
    /// </summary>
    public UIRenderSpace WithView(IUIView view) {
        Views.Add(view);
        if (_html is not null) {
            view.Bind(Controller.MVVM, HTML);
            view.OnBuilt(this);
        }
        else if (Screen.Built)
            _ = HTML;
        return this;
    }
    
    public void MarkViewsDestroyed() {
        foreach (var view in Views)
            view.Unbind();
        Views.Clear();
    }

    //NB: there seems to be a UITK issue where if you hide an element after setting its scaled size to 0,
    // it may not redraw properly when scaled back up to normal size.
    //As such, these hide animations scale to 0.01 (and are set to display:none afterwards).
    private const float MIN_SCALE = 0.01f;
    public record InOutAnimCfg(Func<UIRenderSpace, ICancellee, Task> In, Func<UIRenderSpace, ICancellee, Task> Out) {
        /// <summary>
        /// Check if the state of the render space is at the endpoint of <see cref="Out"/> (or equivalently
        ///  at the startpoint of <see cref="In"/>). If it is, then executing the <see cref="Out"/> anim can be skipped.
        /// </summary>
        public Func<UIRenderSpace, bool> IsEffectivelyHidden { get; init; } = rs => {
            var scale = rs.HTML.transform.scale;
            return (Math.Abs(scale.x) <= MIN_SCALE || Math.Abs(scale.y) <= MIN_SCALE);
        };
    }
    public static readonly InOutAnimCfg TooltipAnim = new(
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.one, 0.4f, Easers.EOutBack, cT),
            rs.HTML.style.FadeTo(1, .25f, cT: cT)),
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(0.01f, 0.01f, 0.01f), 0.25f, null, cT),
            rs.HTML.style.FadeTo(0, .25f, cT: cT)));
    
    public static readonly InOutAnimCfg PopupAnim = new(
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 1, 1), .2f, Easers.EOutSine, cT: cT)),
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 0.01f, 1), .12f, Easers.EIOSine, cT: cT)));
    
    public static readonly InOutAnimCfg FastPopupAnim = new(
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 1, 1), .1f, Easers.EOutSine, cT: cT)),
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 0.01f, 1), .06f, Easers.EIOSine, cT: cT)));

    public UIRenderSpace WithTooltipAnim() {
        AnimCfg = TooltipAnim;
        return this;
    }
    
    public UIRenderSpace WithPopupAnim() {
        AnimCfg = PopupAnim;
        return this;
    }
    
    public UIRenderSpace WithFastPopupAnim() {
        AnimCfg = FastPopupAnim;
        return this;
    }

    public UIRenderSpace UseSourceVisible() {
        VisibleWhenSourcesVisible = true;
        return this;
    }

    public Task MakeTask(params ITransition[] tweens)
        //stepTryPrepend is important so first-frame cancellation takes place immediately
        => tweens.All(t => t.Run(Controller));

    public Task AddSource(UIGroup grp) {
        if (!Sources.Contains(grp)) {
            Sources.Add(grp);
            return (Parent?.AddSource(grp)).And(LocalUpdateVisibility());
        } else
            return Task.CompletedTask;
    }

    public Task RemoveSource(UIGroup grp) {
        if (Sources.Remove(grp)) {
            return (Parent?.RemoveSource(grp)).And(LocalUpdateVisibility());
        } else
            return Task.CompletedTask;
    }

    public UIRenderConstructed Construct(VisualTreeAsset prefab,
        Action<UIRenderConstructed, VisualElement>? builder = null) => new UIRenderConstructed(this, prefab, builder);
    
    public UIRenderColumn Col(int index) => new(this, index);

    /// <summary>
    /// Create a <see cref="UIRenderExplicit"/> that queries for the HTML subtree named `name`.
    /// </summary>
    public UIRenderExplicit Q(string name) => new(this, name);
}

/// <summary>
/// A render space linking to a specific HTML construct in an existing HTML tree.
/// </summary>
public class UIRenderExplicit : UIRenderSpace {
    private readonly Func<VisualElement, VisualElement> htmlFinder;
    protected override VisualElement _FirstLoadHTML() => htmlFinder(Parent!.HTML);

    public UIRenderExplicit(UIRenderSpace parent, Func<VisualElement, VisualElement> htmlFinder) : base(parent.Screen, parent) {
        this.htmlFinder = htmlFinder;
    }
    
    public UIRenderExplicit(UIRenderSpace parent, string htmlName) : base(parent.Screen, parent) {
        this.htmlFinder = ve => ve.Q(htmlName);
    }
}

/// <summary>
/// A render space that renders directly to the screen HTML.
/// </summary>
public class UIRenderScreen : UIRenderSpace {
    protected override VisualElement _FirstLoadHTML() => Screen.HTML;
    protected override bool BaseShouldBeLocalVisible => Screen.State.Value >= UIScreenState.InactiveGoingActive;
    public override bool IsAnimating => Screen.State.Value is > UIScreenState.Inactive and < UIScreenState.Active;
    protected override bool ControlsHTML => false;

    public UIRenderScreen(UIScreen screen) : base(screen, null) {
        screen.Tokens.Add(screen.State.Subscribe(_ => LocalUpdateVisibility()));
    }
}

/// <summary>
/// A render space that renders directly to the screen container.
/// </summary>
public class UIRenderScreenContainer : UIRenderSpace {
    protected override VisualElement _FirstLoadHTML() => Screen.Container;
    protected override bool BaseShouldBeLocalVisible => Screen.State.Value >= UIScreenState.InactiveGoingActive;
    public override bool IsAnimating => Screen.State.Value is > UIScreenState.Inactive and < UIScreenState.Active;
    protected override bool ControlsHTML => false;

    public UIRenderScreenContainer(UIScreen screen) : base(screen, null) {
        screen.Tokens.Add(screen.State.Subscribe(_ => LocalUpdateVisibility()));
    }
}

/// <summary>
/// A render space that renders to the screen's Absolute Territory,
///  which is a darkened overlay that captures mouse clicks.
/// <br/>Normally, the Absolute Territory is hidden, but it becomes visible
///  when a group tries to render to it.
/// <br/>Use this for popups and the like.
/// </summary>
public class UIRenderAbsoluteTerritory : UIRenderSpace {
    private readonly VisualElement absTerr;
    protected override VisualElement _FirstLoadHTML() => absTerr;
    /// <summary>
    /// Alpha of the darkened overlay when fully visible.
    /// Can be overriden by <see cref="UIGroup.OverlayAlphaOverride"/>.
    /// </summary>
    public float Alpha { get; set; } = 0.6f;
    private float GetTargetAlpha() {
        float? a = null;
        foreach (var s in Sources)
            if (s.OverlayAlphaOverride is {} f)
                a = Math.Max(a ?? f, f);
        return a ?? Alpha;
    }

    protected override bool BaseShouldBeLocalVisible => 
        Screen.State.Value >= UIScreenState.InactiveGoingActive && HasVisibleSource;

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen, null) {
        absTerr = Screen.HTML.Query("AbsoluteContainer");
        //TODO: opacity doesn't work correctly? so I'm setting the alpha value manually
        var bgc = absTerr.style.backgroundColor.value;
        absTerr.style.display = DisplayStyle.None;
        absTerr.RegisterCallback<PointerUpEvent>(evt => {
            if (evt.button != 0 || Controller.Current is not {} curr || IsAnimating) 
                return;
            //PointerUpEvent will propagate upwards iff it doesn't hit a UINode (which has StopPropagation).
            //If the node is within a popup, issue a Back unless the propagated pointer event
            // hits the node's lowest containing popup. 
            if (Controller.TargetedPopup == PopupUIGroup.LowestInHierarchy(curr)) 
                return;
            screen.Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Back, null));
            evt.StopPropagation();
        });
        AnimCfg = new((rs, cT) => 
                MakeTask(TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, GetTargetAlpha(), .2f,
                    a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT)),
                (rs, cT) => MakeTask(TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, 0, .12f,
                    a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT))) {
            IsEffectivelyHidden = rs => false
        };
        screen.Tokens.Add(screen.State.Subscribe(_ => LocalUpdateVisibility()));
    }
}

/// <summary>
/// A render space pointing to a .column tree, or a ScrollView that is a child of a .column tree.
/// </summary>
public class UIRenderColumn : UIRenderSpace {
    public int Index { get; }
    protected override VisualElement _FirstLoadHTML() {
        var col = Parent!.HTML.Query(className: "column").ToList()[Index];
        var colScroll = col.Query<ScrollView>().ToList();
        if (colScroll.Count > 0)
            return colScroll[0].FixScrollSize();
        return col;
    }

    public UIRenderColumn(UIScreen screen, int index) : this(screen.ContainerRender, index) { }

    public UIRenderColumn(UIRenderSpace parent, int index) : base(parent.Screen, parent) {
        this.Index = index;
    }
}
/// <summary>
/// A render space pointing to an HTML tree constructed from a prefab.
/// <br/>The dislay will be set to None if no groups are rendering to this.
/// <br/>If using for one-offs like popups, make sure to call Destroy once the popup is cleaned up.
/// </summary>
public class UIRenderConstructed : UIRenderSpace {
    private readonly UIRenderSpace parent;
    private readonly Either<VisualTreeAsset, Func<VisualElement, VisualElement>> prefab;
    private readonly Action<UIRenderConstructed, VisualElement>? builder;
    protected override VisualElement _FirstLoadHTML() {
        VisualElement html;
        if (prefab.TryL(out var vta))
            parent.HTML.Add(html = vta.CloneTreeNoContainer());
        else
            html = prefab.Right(parent.HTML);
        builder?.Invoke(this, html);
        return html;
    }
    protected override bool BaseShouldBeLocalVisible => HasVisibleSource;

    /// <summary>
    /// Constructor for <see cref="UIRenderConstructed"/>.
    /// </summary>
    /// <param name="parent">Parent render space.</param>
    /// <param name="prefab">Either a VisualTreeAsset prefab defining the HTML tree that should be created for this render space,
    ///  or a function that creates an HTML tree provided the parent HTML tree.</param>
    /// <param name="builder">Extra builder options for HTML instantiation.</param>
    public UIRenderConstructed(UIRenderSpace parent, Either<VisualTreeAsset, Func<VisualElement, VisualElement>> prefab, Action<UIRenderConstructed, VisualElement>? builder = null) : base(parent.Screen, parent) {
        this.parent = parent;
        this.prefab = prefab;
        this.builder = builder;
    }

    /// <summary>
    /// Delete this renderer's HTML and clear its sources.
    /// </summary>
    public void Destroy() {
        MarkViewsDestroyed();
        foreach (var s in Sources)
            Parent?.RemoveSource(s);
        Sources.Clear();
        parent.HTML.Remove(HTML);
        Screen.Renderers.Remove(this);
    }
}

}