using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Events;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using Danmokou.Core;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public abstract class UIRenderSpace {
    protected List<UIGroup> Sources { get; } = new();
    protected VisualElement? _html = null;
    public abstract VisualElement HTML { get; }
    public UIScreen Screen { get; }
    public UIRenderSpace? Parent { get; }
    public virtual bool Visible => true;
    public UIController Controller => Screen.Controller;

    public UIRenderSpace(UIScreen screen, UIRenderSpace? parent) {
        this.Screen = screen;
        this.Parent = parent;
    }

    protected void UpdateVisibility() {
        if (_html != null) 
            HTML.style.display = Visible.ToStyle();
    }

    public void AddSource(UIGroup grp) {
        if (!Sources.Contains(grp)) {
            Sources.Add(grp);
            UpdateVisibility();
            Parent?.AddSource(grp);
        }
    }

    public void RemoveSource(UIGroup grp) {
        Sources.Remove(grp);
        UpdateVisibility();
        Parent?.RemoveSource(grp);
    }

    public void SourceBecameVisible(UIGroup grp) {
        UpdateVisibility();
        Parent?.SourceBecameVisible(grp);
    }

    public void SourceBecameHidden(UIGroup grp) {
        UpdateVisibility();
        Parent?.SourceBecameHidden(grp);
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
    private readonly Color bgc;
    private readonly DisturbedOr isTransitioning = new();
    public float Alpha { get; set; } = 0.3f;
    public override bool Visible => Sources.Count > 0;

    public Task FadeIn() {
        var token = isTransitioning.AddConst(true);
        return TransitionHelpers.TweenTo(HTML.style.backgroundColor.value.a, Alpha, 0.1f, 
                a => HTML.style.backgroundColor = bgc.WithA(a))
            .Run(Controller)
            .ContinueWithSync(() => {
                token.Dispose();
            });
    }

    public Task FadeOutIfNoOtherDependencies(UIGroup g) {
        if (Sources.Count == 1 && Sources[0] == g && HTML.style.display == DisplayStyle.Flex) {
            var token = isTransitioning.AddConst(true);
            TransitionHelpers.TweenTo(Alpha, 0, 0.1f, a => HTML.style.backgroundColor = bgc.WithA(a))
                .Run(Controller)
                .ContinueWithSync(() => {
                    token.Dispose();
                });
        }
        return Task.CompletedTask;
    }

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen, null) {
        _html = Screen.HTML.Query("AbsoluteContainer");
        //TODO: this doesn't work correctly, so I'm setting the alpha value manually
        bgc = _html.style.backgroundColor.value;
        _html.style.display = DisplayStyle.None;
        _html.RegisterCallback<PointerUpEvent>(evt => {
            Logs.Log($"Clicked on absolute territory");
            if (screen.Controller.Current == null || isTransitioning) return;
            screen.Controller.QueuedEvent = new UIPointerCommand.NormalCommand(UICommand.Back, null);
            evt.StopPropagation();
        });
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
    private readonly VisualTreeAsset prefab;
    private readonly Action<UIRenderConstructed, VisualElement>? builder;
    public override VisualElement HTML {
        get {
            if (_html == null) {
                parent.HTML.Add(_html = prefab.CloneTreeWithoutContainer());
                builder?.Invoke(this, _html);
                UpdateVisibility();
            }
            return _html;
        }
    }
    public override bool Visible => Sources.Any(g => g.Visible);

    public UIRenderConstructed(UIRenderSpace parent, VisualTreeAsset prefab, Action<UIRenderConstructed, VisualElement>? builder = null) : base(parent.Screen, parent) {
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