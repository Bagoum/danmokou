﻿using System;
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
            }
            return _html;
        }
    }
    public UIScreen Screen { get; }
    public UIRenderSpace? Parent { get; }
    public LazyEvented<bool> IsHTMLVisible { get; }
    protected virtual bool ShouldBeLocalVisible => !VisibleWhenSourcesVisible || HasVisibleSource;
    private bool? ShouldBeVisibleOverride { get; set; }
    public bool ShouldBeVisibleInTree => ShouldBeLocalVisible && (Parent?.ShouldBeVisibleInTree ?? true);
    
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
    /// </summary>
    public bool AnimateOutWithParent { get; set; } = false;

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
    /// Animation played when the render space becomes visible. (Skipped if the render space starts as visible.)
    /// </summary>
    public Func<UIRenderSpace, ICancellee, Task>? IsVisibleAnimation { get; set; }
    
    /// <summary>
    /// Animation played when the render space becomes invisible. (Skipped if the render space starts as invisible.)
    /// </summary>
    public Func<UIRenderSpace, ICancellee, Task>? IsNotVisibleAnimation { get; set; }
    public bool IsFirstRender { get; set; } = true;
    public virtual bool IsAnimating => animateToken?.Cancelled is false;
    protected Cancellable? animateToken;

    public UIRenderSpace(UIScreen screen, UIRenderSpace? parent) {
        this.Screen = screen;
        this.Parent = parent;
        IsHTMLVisible = new(() => _useTreeForHTMLVisibility ? ShouldBeVisibleInTree : ShouldBeLocalVisible);
        Screen.Renderers.Add(this);
    }

    private List<Task> UpdateVisibilityDependents() {
        var tasks = new List<Task>();
        foreach (var dep in VisibilityDependents)
            if (dep.LocalUpdateVisibility() is {IsCompletedSuccessfully:false} t)
                tasks.Add(t);
        return tasks;
    }
    private Task? AnimateIn(bool skip, Func<UIRenderSpace, ICancellee, Task>? entry) {
        HTML.style.display = DisplayStyle.Flex;
        if (!skip && VisibilityDependents.Count > 0) {
            var cT = Cancellable.Replace(ref animateToken);
            var tasks = UpdateVisibilityDependents();
            if (entry is not null)
                tasks.Add(entry(this, cT));
            //cancel cT so IsAnimating becomes false
            return tasks!.All().ContinueWithSync(cT.Cancel);
        } else if (entry is not null) {
            var cT = Cancellable.Replace(ref animateToken);
            if (skip) 
                cT.SoftCancel();
            return entry(this, cT).ContinueWithSync(cT.Cancel);
        } else
            return null;
            
    }

    private Task? AnimateOut(bool skip, Func<UIRenderSpace, ICancellee, Task>? exit) {
        //don't do anything if a parent renderer is disappearing
        if (!skip && !AnimateOutWithParent && Parent?.ShouldBeVisibleInTree is false)
            return null;
        if (!skip && VisibilityDependents.Count > 0) {
            HTML.style.display = DisplayStyle.Flex;
            var tasks = UpdateVisibilityDependents();
            var cT = Cancellable.Replace(ref animateToken);
            if (exit is not null)
                tasks.Add(exit(this, cT));
            return tasks!.All().ContinueWithSync(() => {
                if (!cT.IsHardCancelled())
                    HTML.style.display = DisplayStyle.None;
                cT.Cancel();
            });
        } else if (exit is not null) {
            var cT = Cancellable.Replace(ref animateToken);
            if (skip) 
                cT.SoftCancel();
            else 
                HTML.style.display = DisplayStyle.Flex;
            return exit(this, cT).ContinueWithSync(() => {
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
                t = AnimateIn(skipAnim, IsVisibleAnimation);
            else if (!newVis && (ifr || oldVis))
                t = AnimateOut(skipAnim, IsNotVisibleAnimation);
        }
        return t ?? Task.CompletedTask;
    }
    
    /// <summary>
    /// Add an <see cref="IUIView"/> directly to the HTML for this render space.
    /// </summary>
    public UIRenderSpace WithView(IUIView view) {
        Views.Add(view);
        if (_html is not null)
            view.Bind(Controller.MVVM, HTML);
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
    public static readonly Func<UIRenderSpace, ICancellee, Task> TooltipVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.one, 0.4f, Easers.EOutBack, cT),
            rs.HTML.style.FadeTo(1, .25f, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> TooltipNotVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(0.01f, 0.01f, 0.01f), 0.25f, null, cT),
            rs.HTML.style.FadeTo(0, .25f, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> PopupVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 1, 1), .2f, Easers.EOutSine, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> PopupNotVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 0.01f, 1), .12f, Easers.EIOSine, cT: cT));

    public UIRenderSpace WithTooltipAnim() {
        IsVisibleAnimation = TooltipVisibleAnim;
        IsNotVisibleAnimation = TooltipNotVisibleAnim;
        return this;
    }
    
    public UIRenderSpace WithPopupAnim() {
        IsVisibleAnimation = PopupVisibleAnim;
        IsNotVisibleAnimation = PopupNotVisibleAnim;
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
    protected override bool ShouldBeLocalVisible => Screen.State.Value >= UIScreenState.InactiveGoingActive;
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
    protected override bool ShouldBeLocalVisible => Screen.State.Value >= UIScreenState.InactiveGoingActive;
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
    /// Can be overriden by <see cref="PopupUIGroup.OverlayAlphaOverride"/>.
    /// </summary>
    public float Alpha { get; set; } = 0.6f;
    private float GetTargetAlpha() {
        float? a = null;
        foreach (var s in Sources)
            if (s is PopupUIGroup { OverlayAlphaOverride: { } f })
                a = Math.Max(a ?? f, f);
        return a ?? Alpha;
    }

    protected override bool ShouldBeLocalVisible => 
        Screen.State.Value >= UIScreenState.InactiveGoingActive && HasVisibleSource;

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen, null) {
        absTerr = Screen.HTML.Query("AbsoluteContainer");
        //TODO: opacity doesn't work correctly? so I'm setting the alpha value manually
        var bgc = absTerr.style.backgroundColor.value;
        absTerr.style.display = DisplayStyle.None;
        absTerr.RegisterCallback<PointerUpEvent>(evt => {
            if (evt.button != 0 || screen.Controller.Current == null || IsAnimating) return;
            //Logs.Log("Clicked on absolute territory");
            screen.Controller.QueueInput(new UIPointerCommand.NormalCommand(UICommand.Back, null));
            evt.StopPropagation();
        });
        IsVisibleAnimation = (rs, cT) => MakeTask(
            TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, GetTargetAlpha(), .2f,
                a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT));
        IsNotVisibleAnimation = (rs, cT) => MakeTask(
            TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, 0, .12f,
                a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT));
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
    protected override bool ShouldBeLocalVisible => HasVisibleSource;

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