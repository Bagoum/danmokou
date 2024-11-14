using System;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Tasks;
using Danmokou.Services;
using Danmokou.UI.XML;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;
using UnityEngine.UIElements;

namespace Danmokou.VN {
public static class VNUtils {
    public static OptionSelector<C> SetupSelector<C>(IVNState vn, XMLDynamicMenu menu, Func<C, LString?> displayer,
        out IDisposable token) {
        var selector = new OptionSelector<C>(vn);
        
        var (optsScreen, freeform) = menu.MakeScreen(null, (s, ve) => {
                //don't let events fall-through
                s.HTML.pickingMode = PickingMode.Position;
                s.Margin.SetLRMargin(720, 720);
                var c0 = ve.AddColumn();
                c0.style.maxWidth = 100f.Percent();
                c0.style.maxHeight = 60f.Percent();
                c0.style.marginTop = 100;
                c0.style.alignItems = Align.Center;
                c0.style.justifyContent = Justify.SpaceAround;
            });
        optsScreen.AllowsPlayerExit = false;
        var optGroup = new UIColumn(optsScreen, null);
        freeform.AddGroupDynamic(optGroup);
        
        UINode[]? optNodes = null;
        void DestroyOptionNodes() {
            // ReSharper disable method AccessToModifiedClosure
            if (optNodes != null)
                foreach (var n in optNodes)
                    n.Remove();
            optNodes = null;
        }
        token = selector.CurrentRequest.Subscribe(req => {
            if (req != null) {
                DestroyOptionNodes();
                optNodes = req.Options.Select((o, i) => {
                    if (displayer(o) is not { } ls)
                        return null; //treat node as hidden
                    var n = new FuncNode(ls, () => {
                        req.Select(i);
                        return new UIResult.ReturnToScreenCaller() { OnPostTransition = DestroyOptionNodes };
                    }) { Prefab = GameManagement.UXMLPrefabs.OptionsColumnUINode };
                    optGroup.AddNodeDynamic(n);
                    return n as UINode;
                }).FilterNone().ToArray();
                menu.OperateOnResult(new UIResult.GoToScreen(optsScreen, menu.Unselect), new() {
                    ScreenTransitionTime = 0.7f, DelayScreenFadeInRatio = 0
                }).Log();
            }
        });
        return selector;
    }
}
}