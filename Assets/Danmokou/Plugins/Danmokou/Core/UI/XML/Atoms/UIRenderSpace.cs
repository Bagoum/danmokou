using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    protected VisualElement? _html = null;
    public abstract VisualElement HTML { get; }
    public UIScreen Screen { get; }
    public UIRenderSpace? Parent { get; }
    public LazyEvented<bool> IsVisible { get; }
    protected virtual bool ShouldBeVisibleBase => true;
    private bool? ShouldBeVisibleOverride { get; set; }
    protected bool ShouldBeVisible => ShouldBeVisibleOverride ?? ShouldBeVisibleBase;
    public bool ShouldBeVisibleInTree => ShouldBeVisible && (Parent?.ShouldBeVisible ?? true);
    public bool HasVisibleSource {
        get {
            for (int ii = 0; ii < Sources.Count; ++ii)
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
        for (int ii = 0; ii < Sources.Count; ++ii)
            if (!Sources[ii].Hierarchy.IsWeakPrefix(gh))
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
    public bool IsAnimating => animateToken?.Cancelled is false;
    protected ICancellable? animateToken;

    public UIRenderSpace(UIScreen screen, UIRenderSpace? parent) {
        this.Screen = screen;
        this.Parent = parent;
        IsVisible = new(() => ShouldBeVisible);
    }

    private async Task AnimateIn(bool first, Func<UIRenderSpace, ICancellee, Task> entry) {
        animateToken?.Cancel();
        var cT = animateToken = new Cancellable();
        if (first) cT.SoftCancel();
        HTML.style.display = DisplayStyle.Flex;
        await entry(this, cT);
        if (animateToken == cT)
            animateToken = null;
    }

    private async Task AnimateOut(bool first, Func<UIRenderSpace, ICancellee, Task> exit) {
        animateToken?.Cancel();
        var cT = animateToken = new Cancellable();
        if (first) cT.SoftCancel();
        else HTML.style.display = DisplayStyle.Flex;
        await exit(this, cT);
        HTML.style.display = DisplayStyle.None;
        if (animateToken == cT)
            animateToken = null;
    }

    public Task OverrideVisibility(bool? visible) {
        ShouldBeVisibleOverride = visible;
        return UpdateVisibility();
    }

    public void FastUpdateVisibility() => UpdateVisibility(true);

    protected Task UpdateVisibility(bool fastUpdate = false) {
        var oldVis = IsVisible.Value;
        IsVisible.Recompute();
        var newVis = IsVisible.Value;
        Task? t = null;
        if (_html != null) {
            var skipAnim = IsFirstRender || fastUpdate;
            IsFirstRender = false;
            if (newVis && (skipAnim || !oldVis) && IsVisibleAnimation is {} entry) {
                t = AnimateIn(skipAnim, entry);
            } else if (!newVis && (skipAnim || oldVis) && IsNotVisibleAnimation is { } exit) {
                t = AnimateOut(skipAnim, exit);
            } else if ((newVis != oldVis && Parent?.ShouldBeVisibleInTree is not false) || skipAnim)
                //don't directly modify display if a parent renderer is disappearing
                HTML.style.display = newVis.ToStyle();
        }
        return t.And(Parent?.UpdateVisibility(fastUpdate));
    }

    public static readonly Func<UIRenderSpace, ICancellee, Task> TooltipVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.one, 0.4f, Easers.EOutBack, cT),
            rs.HTML.style.FadeTo(1, .25f, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> TooltipNotVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.zero, 0.25f, null, cT),
            rs.HTML.style.FadeTo(0, .25f, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> PopupVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 1, 1), .2f, Easers.EOutSine, cT: cT));
    
    public static readonly Func<UIRenderSpace, ICancellee, Task> PopupNotVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(new Vector3(1, 0, 1), .12f, Easers.EIOSine, cT: cT));

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

    public Task MakeTask(params ITransition[] tweens) =>
        //stepTryPrepend is important so first-frame cancellation takes place immediately
        Task.WhenAll(tweens.Select(t => t.Run(Controller)));

    public Task AddSource(UIGroup grp) {
        if (!Sources.Contains(grp)) {
            Sources.Add(grp);
            return UpdateVisibility().And(Parent?.AddSource(grp));
        } else
            return Task.CompletedTask;
    }

    public Task RemoveSource(UIGroup grp) {
        if (Sources.Remove(grp)) {
            return UpdateVisibility().And(Parent?.RemoveSource(grp));
        } else
            return Task.CompletedTask;
    }

    public Task SourceBecameVisible(UIGroup grp) {
        return UpdateVisibility().And(Parent?.SourceBecameVisible(grp));
    }

    public Task SourceBecameHidden(UIGroup grp) {
        return UpdateVisibility().And(Parent?.SourceBecameHidden(grp));
    }

    public UIRenderConstructed Construct(VisualTreeAsset prefab,
        Action<UIRenderConstructed, VisualElement>? builder = null) => new UIRenderConstructed(this, prefab, builder);
    
    public UIRenderColumn ColumnRender(int index) => new(this, index);
}

/// <summary>
/// A render space linking to a specific HTML construct in an existing HTML tree.
/// </summary>
public class UIRenderExplicit : UIRenderSpace {
    private readonly Func<VisualElement, VisualElement> htmlFinder;
    public override VisualElement HTML => _html ??= htmlFinder(Parent!.HTML);

    public UIRenderExplicit(UIRenderSpace parent, Func<VisualElement, VisualElement> htmlFinder) : base(parent.Screen, parent) {
        this.htmlFinder = htmlFinder;
    }
}

/// <summary>
/// A render space that renders directly to the screen HTML.
/// </summary>
public class UIRenderScreen : UIRenderSpace {
    public override VisualElement HTML => _html ??= Screen.HTML;
    protected override bool ShouldBeVisibleBase => Screen.ScreenIsActive;

    public UIRenderScreen(UIScreen screen) : base(screen, null) {
        screen.Tokens.Add(screen.OnEnterStart.Subscribe(_ => UpdateVisibility()));
        screen.Tokens.Add(screen.OnExitEnd.Subscribe(_ => UpdateVisibility()));
    }
}

/// <summary>
/// A render space that renders directly to the screen container.
/// </summary>
public class UIRenderScreenContainer : UIRenderSpace {
    public override VisualElement HTML => _html ??= Screen.Container;
    protected override bool ShouldBeVisibleBase => Screen.ScreenIsActive;

    public UIRenderScreenContainer(UIScreen screen) : base(screen, null) {
        screen.Tokens.Add(screen.OnEnterStart.Subscribe(_ => UpdateVisibility()));
        screen.Tokens.Add(screen.OnExitEnd.Subscribe(_ => UpdateVisibility()));
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
    public override VisualElement HTML => _html!;
    /// <summary>
    /// Alpha of the darkened overlay when fully visible.
    /// Can be overriden by <see cref="PopupUIGroup.OverlayAlphaOverride"/>.
    /// </summary>
    public float Alpha { get; set; } = 0.5f;
    private float GetTargetAlpha() {
        float? a = null;
        for (int ii = 0; ii < Sources.Count; ++ii)
            if (Sources[ii] is PopupUIGroup { OverlayAlphaOverride: { } f })
                a = Math.Max(a ?? f, f);
        return a ?? Alpha;
    }

    protected override bool ShouldBeVisibleBase => Screen.ScreenIsActive && HasVisibleSource;

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen, null) {
        _html = Screen.HTML.Query("AbsoluteContainer");
        //TODO: opacity doesn't work correctly? so I'm setting the alpha value manually
        var bgc = _html.style.backgroundColor.value;
        _html.style.display = DisplayStyle.None;
        _html.RegisterCallback<PointerUpEvent>(evt => {
            if (evt.button != 0 || screen.Controller.Current == null || animateToken?.Cancelled is false) return;
            Logs.Log("Clicked on absolute territory");
            screen.Controller.QueueEvent(new UIPointerCommand.NormalCommand(UICommand.Back, null));
            evt.StopPropagation();
        });
        IsVisibleAnimation = (rs, cT) => {
            return MakeTask(
                TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, GetTargetAlpha(), .2f,
                    a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT));
        };
        IsNotVisibleAnimation = (rs, cT) => {
            return MakeTask(
                TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, 0, .12f,
                    a => HTML.style.backgroundColor = Helpers.WithA(bgc, a), cT: cT));
        };
        //_ = UpdateVisibility().ContinueWithSync();
        screen.Tokens.Add(screen.OnEnterStart.Subscribe(_ => UpdateVisibility()));
        screen.Tokens.Add(screen.OnExitEnd.Subscribe(_ => UpdateVisibility()));
    }
}

/// <summary>
/// A render space pointing to a .column tree, or a ScrollView that is a child of a .column tree.
/// </summary>
public class UIRenderColumn : UIRenderSpace {
    public int Index { get; }

    private VisualElement Column {
        get {
            var col = Parent!.HTML.Query(className: "column").ToList()[Index];
            var colScroll = col.Query<ScrollView>().ToList();
            if (colScroll.Count > 0)
                return colScroll[0];
            return col;
        }
    }
    public override VisualElement HTML => _html ??= Column;

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
    public override VisualElement HTML {
        get {
            if (_html == null) {
                if (prefab.TryL(out var vta))
                    parent.HTML.Add(_html = vta.CloneTreeNoContainer());
                else
                    _html = prefab.Right(parent.HTML);
                builder?.Invoke(this, _html);
                _ = UpdateVisibility().ContinueWithSync();
            }
            return _html;
        }
    }
    protected override bool ShouldBeVisibleBase => HasVisibleSource;

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
        for (int ii = 0; ii < Sources.Count; ++ii)
            Parent?.RemoveSource(Sources[ii]);
        Sources.Clear();
        parent.HTML.Remove(HTML);
    }
}

}