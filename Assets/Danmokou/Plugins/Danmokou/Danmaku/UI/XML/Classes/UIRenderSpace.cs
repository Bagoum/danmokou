using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Events;
using BagoumLib.Tasks;
using BagoumLib.Tweening;
using Danmokou.Core;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {
public abstract class UIRenderSpace {
    protected List<UIGroup> Groups { get; } = new();
    protected VisualElement? _html = null;
    public abstract VisualElement HTML { get; }
    public UIScreen Screen { get; }
    public UIController Controller => Screen.Controller;

    public UIRenderSpace(UIScreen screen) {
        this.Screen = screen;
    }
    
    public virtual void AddGroup(UIGroup grp) => Groups.Add(grp);
    
    public virtual void RemoveGroup(UIGroup grp) => Groups.Remove(grp);
    public void HideAllGroups() {
        foreach (var g in Groups)
            g.Hide();
    }
}

public class UIRenderExplicit : UIRenderSpace {
    private readonly Func<VisualElement, VisualElement> htmlFinder;
    public override VisualElement HTML => _html ??= htmlFinder(Screen.HTML);
    public UIRenderExplicit(UIScreen screen, Func<VisualElement, VisualElement> htmlFinder) : base(screen) {
        this.htmlFinder = htmlFinder;
    }
}
public class UIRenderDirect : UIRenderSpace {
    public override VisualElement HTML => _html ??= Screen.Container;
    public UIRenderDirect(UIScreen screen) : base(screen) { }
}
public class UIRenderAbsoluteTerritory : UIRenderSpace {
    public override VisualElement HTML => _html!;
    private readonly Color bgc;
    private readonly DisturbedOr isTransitioning = new();
    public float Alpha { get; set; } = 0.3f;

    public Task FadeIn() {
        var token = isTransitioning.AddConst(true);
        return Tween.TweenTo(HTML.style.backgroundColor.value.a, Alpha, 0.1f, 
                a => HTML.style.backgroundColor = bgc.WithA(a))
            .Run(Controller)
            .ContinueWithSync(() => {
                token.Dispose();
            });
    }

    public Task FadeOutIfNoOtherDependencies(UIGroup g) {
        if (Groups.Count == 1 && Groups[0] == g && HTML.style.display == DisplayStyle.Flex) {
            var token = isTransitioning.AddConst(true);
            Tween.TweenTo(Alpha, 0, 0.1f, a => HTML.style.backgroundColor = bgc.WithA(a))
                .Run(Controller)
                .ContinueWithSync(() => {
                    token.Dispose();
                });
        }
        return Task.CompletedTask;
    }
    private void VerifyHTMLDisplay() {
        //The absolute territory captures events. We can use this to catch "out of popup" clicks when a popup is open,
        // but when no popups are active, it's in the way.
        HTML.style.display = (Groups.Count > 0) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public UIRenderAbsoluteTerritory(UIScreen screen) : base(screen) {
        _html = Screen.HTML.Query("AbsoluteContainer");
        //TODO: this doesn't work correctly, so I'm setting the alpha value manually
        bgc = _html.style.backgroundColor.value;
        _html.style.display = DisplayStyle.None;
        _html.RegisterCallback<MouseUpEvent>(evt => {
            Logs.Log($"Clicked on absolute territory");
            if (screen.Controller.Current == null || isTransitioning) return;
            screen.Controller.QueuedEvent = new UIMouseCommand.NormalCommand(UICommand.Back, null);
            evt.StopPropagation();
        });
    }

    public override void AddGroup(UIGroup grp) {
        base.AddGroup(grp);
        VerifyHTMLDisplay();
    }

    public override void RemoveGroup(UIGroup grp) {
        base.RemoveGroup(grp);
        VerifyHTMLDisplay();
    }
}

public class UIRenderColumn : UIRenderSpace {
    public int Index { get; }

    private VisualElement Column {
        get {
            var col = Screen.HTML.Query(className: "column").ToList()[Index];
            var colScroll = col.Query<ScrollView>().ToList();
            if (colScroll.Count > 0)
                return colScroll[0];
            return col;
        }
    }
    public override VisualElement HTML => _html ??= Column;

    public UIRenderColumn(UIScreen screen, int index) : base(screen) {
        this.Index = index;
    }
}
/*
public class UIRenderConstructed : UIRenderSpace {
    private readonly UIRenderSpace parent;
    private readonly VisualTreeAsset prefab;
    public override VisualElement HTML {
        get {
            if (_html == null)
                parent.HTML.Add(_html = prefab.CloneTreeWithoutContainer());
            return _html;
        }
    }

    public UIRenderConstructed(UIRenderSpace parent, VisualTreeAsset prefab) : base(parent.Screen) {
        this.parent = parent;
        this.prefab = prefab;
    }

    public void Destroy() {
        if (Groups.Count > 0)
            throw new Exception("Cannot destroy a RenFderSpace when it has active groups");
        parent.HTML.Remove(HTML);
    }
}
*/
}