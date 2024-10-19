using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.DMath;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

//wip tests for dynamic animations on an informational menu
public class LocalXMLTesting : CoroutineRegularUpdater {
    //in unity units
    public Vector2 Size = Vector2.one;
    public Vector2 Offset;
    public FixedXMLObject XML { get; private set; } = null!;
    public Sprite testIcon = null!;

    public bool[] visible = new bool[4];

    public override void FirstFrame() {
        var xmlLoc = UIBuilderRenderer.UICamInfo.WorldToXML((Vector2)transform.position + Offset);
        XML = new(xmlLoc.x, xmlLoc.y, null, null);
        var menu = ServiceLocator.Find<XMLDynamicMenu>();
        //var node = new UINode("hello world") { Prefab = XMLUtils.Prefabs.PureTextNode };
        //node.ConfigureAbsoluteLocation(XML);
        //node.MakeTooltip((menu as IFixedXMLObjectContainer).Screen, "foobar");
        //menu.AddNodeDynamic(node);
        var data = new[] {
            ("hello world", 1234567),
            ("foo bar", 34567890),
            ("xyz", 12),
            ("abc", 2305)
        };
        var nodes = data.Select((d, i) => {
            var show = "";
            var node = new TwoLabelUINode(d.Item1, () => show, null) {
                VisibleIf = () => visible[i]
            }.WithCSS(XMLUtils.noPointerClass, XMLUtils.highVisClass);
            node.RootView.OnFirstRender((n, cT) => {
                var delta = (Vector3)UIBuilderRenderer.ToUIXMLDims(new(0, 5));
                n.HTML.transform.position -= delta;
                return new NoopTweener(0.2f * i, cT).Then(
                    n.HTML.transform.TranslateBy(delta, 1.4f, Easers.EOutBack, cT).Parallel(
                        new NoopTweener(0.4f, cT).Then(new Tweener<float>(0, d.Item2, 2f,
                            f => { show = ((int)Math.Round(f)).ToString(); }, Easers.EOutQuart, cT))));
            });
            return node;
        });


        menu.FreeformGroup.AddGroupDynamic(new UIColumn(
            new UIRenderConstructed(menu.FreeformGroup.Render, new(XMLUtils.AddColumn), (_, ve) => {
                ve.ConfigureAbsolute(XMLUtils.Pivot.Top);
                ve.style.top = 400;
                ve.style.left = 1900;
                ve.style.width = 26f.Percent();
                ve.style.height = new StyleLength(StyleKeyword.Auto);
                ve.SetPadding(30, 50, 30, 50);
                new Tweener<float>(0, 0.6f, 1f, a => {
                    ve.style.backgroundColor = new Color(0.32f, 0.22f, 0.26f, a);
                    ve.SetBorder(new Color(0.35f, 0.26f, 0.33f, a), 7);
                }).Run(menu, CoroutineOptions.DroppableDefault);
            }).Col(0), nodes
        ) {
            Interactable = false
        });
    }


#if UNITY_EDITOR
    private void OnDrawGizmos() {
        var position = (Vector2)transform.position;
        Handles.color = Color.green;
        Handles.DrawSolidRectangleWithOutline(new Rect((position + Offset) - Size / 2, Size),
            Color.clear, Color.green);
    }
#endif
}