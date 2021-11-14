﻿using System;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.UI.XML;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI {
public abstract class SubmenuHandler : CoroutineRegularUpdater {
    public abstract UIScreen Initialize(XMLMainMenu menu);
}

public abstract class IndexedSubmenuHandler : SubmenuHandler {
    protected abstract int NumOptions { get; }
    protected virtual int DefaultOption => 0;
    protected XMLMainMenu Menu { get; private set; } = null!;

    public override UIScreen Initialize(XMLMainMenu menu) {
        Menu = menu;
        HideOnExit();
        var opt = new OptionNodeLR<int>(LString.Empty, SetIndex, NumOptions.Range().ToArray(), DefaultOption) {
            Navigator = (n, req) => req switch {
                UICommand.Up => n.Navigate(UICommand.Left),
                UICommand.Down => n.Navigate(UICommand.Right),
                UICommand.Confirm => Activate((n as OptionNodeLR<int>)!.Value),
                _ => null
            },
        };
        var screen = new UIScreen(menu, null, UIScreen.Display.Unlined) {
            OnEnterStart = () => {
                OnPreEnter(opt.Value);
                Show(opt.Value, true);
            },
            OnExitStart = OnPreExit,
            OnExitEnd = HideOnExit
        };
        _ = new UIColumn(screen, null, opt);
        return screen;
    }

    protected virtual void SetIndex(int index) => Show(index, false);
    
    protected virtual void OnPreExit() { }
    protected virtual void OnPreEnter(int index) { }
    protected abstract void HideOnExit();

    protected abstract void Show(int index, bool isOnEnter);

    protected abstract UIResult Activate(int index);
}
}