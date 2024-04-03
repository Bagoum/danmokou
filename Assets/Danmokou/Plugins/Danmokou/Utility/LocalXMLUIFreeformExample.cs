using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

public class LocalXMLUIFreeformExample : CoroutineRegularUpdater {
    
    private XMLDynamicMenu menu = null!;
    private UIGroup g1 = null!;
    public override void FirstFrame() {
        menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = menu.MainScreen;
        g1 = new UIColumn(new UIRenderExplicit(s, _ => {
            var col = s.Container.AddColumn();
            col.ConfigureAbsolute().WithAbsolutePosition(1920, 1080);
            col.style.width = 800;
            col.style.height = 600;
            col.style.justifyContent = Justify.Center;
            return col;
        }), new UINode("hello world"));
        menu.FreeformGroup.AddGroupDynamic(g1);
        g1.AddNodeDynamic(new UINode("foobar"));
        g1.AddNodeDynamic(new UINode("this node has a tooltip")
            .Bind(new TooltipView(new("this is a tooltip!"))));
        g1.AddNodeDynamic(new FuncNode("this node has a popup", n => {
            var p = PopupUIGroup.CreatePopup(n, "Poffpup",
                r => new UIColumn(r, new UINode("basic popup description")
                        { Prefab = Prefabs.PureTextNode })
                    { Interactable = false },
                new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                    new UIButton("OK", UIButton.ButtonType.Confirm, UIButton.GoBackCommand(n))
                }));
            return p;
        }));
        g1.AddNodeDynamic(new UINode("this node has a menu (C)")
            .Bind(new TooltipView(new("this is a tooltip!")))
            .Bind(new ContextMenuView(new(ContextMenu))));

        UINode[] ContextMenu(UINode n, ICursorState cs) {
            return new[] {
                new UINode("another one").Bind(new ContextMenuView(new(ContextMenu))),
                new FuncNode("go to previous node", () => new UIResult.GoToNode(n.Group, n.Group.Nodes.IndexOf(n) - 1)),
                new FuncNode("delete this node", () => {
                    var ind = n.Group.Nodes.IndexOf(n);
                    n.Remove();
                    return new UIResult.GoToNode(n.Group, ind);
                })
            };
        }
    }

    [ContextMenu("Add group to right")]
    public void AddGroupToRight() {
        var s = menu.MainScreen;
        var g = new UIColumn(new UIRenderExplicit(s, _ => {
            var col = s.Container.AddColumn();
            col.ConfigureAbsolute().WithAbsolutePosition(2600, 1080);
            col.style.width = 1000;
            col.style.height = 600;
            col.style.justifyContent = Justify.FlexStart;
            return col;
        }), new UINode("node 1"));
        menu.FreeformGroup.AddGroupDynamic(g);
    }

    [ContextMenu("Add scale in node")]
    public void AddNodeWithScaleIn() {
        var n = new UINode("this will scale in");
        n.RootView.OnFirstRender((n, cT) => {
            //I don't think there's a good way to handle scaling in an object that doesn't have a fixed initial size
            // Negative percent margin doesn't work as it's relative to *parent width*, and transform scale on its own
            // only affects visual display, not layout.
            //If you do have a fixed initial size, you can handle it like this (in this example, just y-scaling):
            var h = 120f;
            n.HTML.style.height = h;
            return TransitionHelpers.TweenTo(0f, 1f, 1.5f, y => {
                n.HTML.transform.scale = new(1, y, 1);
                n.HTML.style.marginTop = n.HTML.style.marginBottom = (1 - y) * h / -2f;
            }, Easers.EOutSine, cT);
        });
        g1.AddNodeDynamic(n);
    }
}
