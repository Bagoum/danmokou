using System;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.Tasks;
using Danmokou.Services;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;
using UnityEngine.UIElements;

namespace Danmokou.VN {
public static class VNUtils {
    public static SelectionRequest<C> SetupSelector<C>(IVNState vn, XMLDynamicMenu menu, Func<C, LString> displayer,
        out IDisposable token) {
        var selector = new SelectionRequest<C>(vn);
        
        var optsScreen = new UIScreen(menu, null, UIScreen.Display.Unlined) { 
            Builder = (s, ve) => {
                //don't let events fall-through
                s.HTML.pickingMode = PickingMode.Position;
                s.Margin.SetLRMargin(720, 720);
                var c0 = ve.AddColumn();
                c0.style.maxWidth = 100f.Percent();
                c0.style.maxHeight = 60f.Percent();
                c0.style.marginTop = 100;
                c0.style.alignItems = Align.Center;
                c0.style.justifyContent = Justify.SpaceAround;
            },
            UseControlHelper = false,
            AllowsPlayerExit = false
        };
        menu.AddScreen(optsScreen);
        var optGroup = new UIColumn(optsScreen, null);
        optsScreen.SetFirst(optGroup);
        
        UINode[]? optNodes = null;
        void DestroyOptionNodes() {
            if (optNodes != null) {
                foreach (var n in optNodes)
                    n.Remove();
            }
            optNodes = null;
        }
        token = selector.RequestChanged.Subscribe(opts => {
            if (opts != null) {
                DestroyOptionNodes();
                optNodes = opts.Select((o, i) => {
                    var n = new FuncNode(displayer(o), () => {
                        selector.MakeSelection(i);
                        return new UIResult.ReturnToScreenCaller() { OnPostTransition = DestroyOptionNodes };
                    }) { Prefab = GameManagement.UXMLPrefabs.OptionsColumnUINode };
                    optGroup.AddNodeDynamic(n);
                    return n as UINode;
                }).ToArray();
                menu.OperateOnResult(new UIResult.GoToScreen(optsScreen, menu.Unselect), new() {
                    ScreenTransitionTime = 1f, DelayScreenFadeIn = false
                }).ContinueWithSync();
            }
        });
        return selector;
    }
}
}