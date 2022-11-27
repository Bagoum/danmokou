using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

public class TmpTestUIFreeform : CoroutineRegularUpdater {
    /*
    private XMLDynamicMenu menu;
    private UIGroup g;
    public override void FirstFrame() {
        menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = menu.MainScreen;
        g = new UIColumn(new UIRenderExplicit(s, _ => {
            var col = menu.MainScreen.Container.AddColumn();
            col.ConfigureAbsolute();
            col.style.width = 1304;
            col.style.height = 600;
            col.style.top = 1080;
            col.style.left = 1920;
            col.style.justifyContent = Justify.Center;
            return col;
        }), new UINode("hello world"));
        menu.FreeformGroup.AddGroupDynamic(g);
        g.AddNodeDynamic(new UINode("foobar"));
        menu.Redraw();
    }

    [ContextMenu("Add node")]
    public void AddAnotherNode() {
        var n = new UINode("this will scale in") {
            OnFirstRender = n => {
                //I don't think there's a good way to handle scaling in an object that doesn't have a fixed initial size
                // Negative percent margin doesn't work as it's relative to *parent width*, and transform scale on its own
                // only affects visual display, not layout.
                //If you do have a fixed initial size, you can handle it like this (in this example, just y-scaling):
                var h = 120f;
                n.HTML.style.height = h;
                TransitionHelpers.TweenTo(0f, 1f, 1.5f, y => {
                    n.HTML.transform.scale = new(1, y, 1);
                    n.HTML.style.marginTop = n.HTML.style.marginBottom = (1 - y) * h / -2f;
                }, Easers.EOutSine).Run(menu);
            }
        };
        g.AddNodeDynamic(n);
        
    }*/
}
