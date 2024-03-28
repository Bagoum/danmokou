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
    public virtual bool ShouldBeVisible => true;
    public UIController Controller => Screen.Controller;
    
    /// <summary>
    /// Animation played when the render space becomes visible. (Skipped if the render space starts as visible.)
    /// </summary>
    public Func<UIRenderSpace, ICancellee, Task>? IsVisibleAnimation { get; set; }
    
    /// <summary>
    /// Animation played when the render space becomes invisible. (Skipped if the render space starts as invisible.)
    /// </summary>
    public Func<UIRenderSpace, ICancellee, Task>? IsNotVisibleAnimation { get; set; }
    private bool isFirstRender = true;
    public ICancellable? AnimateToken { get; private set; }

    public UIRenderSpace(UIScreen screen, UIRenderSpace? parent) {
        this.Screen = screen;
        this.Parent = parent;
        IsVisible = new(() => ShouldBeVisible);
    }

    protected async Task UpdateVisibility() {
        var oldVis = IsVisible.Value;
        IsVisible.Recompute();
        var newVis = IsVisible.Value;
        if (_html != null) {
            var first = isFirstRender;
            isFirstRender = false;
            if (newVis && (first || !oldVis) && IsVisibleAnimation is {} entry) {
                AnimateToken?.Cancel();
                var cT = AnimateToken = new Cancellable();
                if (first) cT.SoftCancel();
                HTML.style.display = DisplayStyle.Flex;
                await entry(this, cT);
                if (AnimateToken == cT)
                    AnimateToken = null;
            } else if (!newVis && (first || oldVis) && IsNotVisibleAnimation is { } exit) {
                AnimateToken?.Cancel();
                var cT = AnimateToken = new Cancellable();
                if (first) cT.SoftCancel();
                else HTML.style.display = DisplayStyle.Flex;
                await exit(this, cT);
                HTML.style.display = DisplayStyle.None;
                if (AnimateToken == cT)
                    AnimateToken = null;
            } else if (newVis != oldVis || first)
                HTML.style.display = newVis.ToStyle();
        }
    }

    public static Func<UIRenderSpace, ICancellee, Task> TooltipVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.one, 0.4f, Easers.EOutBack, cT),
            rs.HTML.style.FadeTo(1, .25f, cT: cT));
    
    public static Func<UIRenderSpace, ICancellee, Task> TooltipNotVisibleAnim =
        (rs, cT) => rs.MakeTask(rs.HTML.transform.ScaleTo(Vector3.zero, 0.25f, null, cT),
            rs.HTML.style.FadeTo(0, .25f, cT: cT));

    public UIRenderSpace WithTooltipAnim() {
        IsVisibleAnimation = TooltipVisibleAnim;
        IsNotVisibleAnimation = TooltipNotVisibleAnim;
        return this;
    }

    public Task MakeTask(params ITransition[] tweens) =>
        //stepTryPrepend is important so first-frame cancellation takes place immediately
        Task.WhenAll(tweens.Select(t => t.Run(Controller, new(true, CoroutineType.StepTryPrepend))));

    public async Task AddSource(UIGroup grp) {
        if (!Sources.Contains(grp)) {
            Sources.Add(grp);
            await UpdateVisibility();
            Parent?.AddSource(grp);
        }
    }

    public async Task RemoveSource(UIGroup grp) {
        Sources.Remove(grp);
        await UpdateVisibility();
        Parent?.RemoveSource(grp);
    }

    public async Task SourceBecameVisible(UIGroup grp) {
        var t = UpdateVisibility();
        await (Parent?.SourceBecameVisible(grp) ?? Task.CompletedTask);
        await t;
    }

    public async Task SourceBecameHidden(UIGroup grp) {
        var t = UpdateVisibility();
        await (Parent?.SourceBecameHidden(grp) ?? Task.CompletedTask);
        await t;
    }

    public UIRenderConstructed Construct(VisualTreeAsset prefab,
        Action<UIRenderConstructed, VisualElement>? builder = null) => new UIRenderConstructed(this, prefab, builder);
    
    public UIRenderColumn ColumnRender(int index) => new(this, index);
}

/// <summary>
/// A render space linking to a specific HTML construct in the screen's HTML tree.
/// </summary>
public class UIRenderExplicit : UIRenderSpace {
    private readonly Func<VisualElement, VisualElement> htmlFinder;
    public override VisualElement HTML => _html ??= htmlFinder(Screen.HTML);
    public UIRenderExplicit(UIScreen screen, Func<VisualElement, VisualElement> htmlFinder) : base(screen, null) {
        this.htmlFinder = htmlFinder;
    }
    
    public UIRenderExplicit(UIScreen screen, UINode parent) : this(screen, _ => parent.BodyOrNodeHTML) { }
}
/// <summary>
/// A render space that renders directly to the screen container.
/// </summary>
public class UIRenderDirect : UIRenderSpace {
    public override VisualElement HTML => _html ??= Screen.Container;
    public UIRenderDirect(UIScreen screen) : base(screen, null) { }
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
    public override bool ShouldBeVisible => Sources.Count > 0 && Sources.Any(g => g.Visible);

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen, null) {
        _html = Screen.HTML.Query("AbsoluteContainer");
        //TODO: opacity doesn't work correctly? so I'm setting the alpha value manually
        var bgc = _html.style.backgroundColor.value;
        _html.style.display = DisplayStyle.None;
        _html.RegisterCallback<PointerUpEvent>(evt => {
            if (evt.button != 0 || screen.Controller.Current == null || AnimateToken?.Cancelled is false) return;
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
        _ = UpdateVisibility().ContinueWithSync();
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

    public UIRenderColumn(UIScreen screen, int index) : this(screen.DirectRender, index) { }

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
    public override bool ShouldBeVisible => Sources.Count > 0 && Sources.Any(g => g.Visible);

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

    public void Destroy() {
        if (Sources.Count > 0)
            throw new Exception("Cannot destroy a RenderSpace when it has active groups");
        parent.HTML.Remove(HTML);
    }
}

}