using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.SM;
using Danmokou.UI;
using TMPro;
using UnityEngine;
using static Danmokou.Core.InputManager;
using static Danmokou.Core.LocalizedStrings.Tutorial;

namespace Danmokou.Services {
public class MiniTutorial : BehaviorEntity {
    // Start is called before the first frame update
    public TextMeshPro text00 = null!;
    public TextMeshPro text10 = null!;
    public Color prompt;
    public Color message;
    private readonly Dictionary<TextMeshPro, Vector2> defaultLoc = new Dictionary<TextMeshPro, Vector2>();

    protected override void Awake() {
        base.Awake();
        defaultLoc[text00] = text00.transform.localPosition;
        defaultLoc[text10] = text10.transform.localPosition;
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService(this);
    }

    public void RunMiniTutorial(Action cb) => RunDroppableRIEnumerator(RunTutorial(cb));

    private void ClearText() {
        text00.text = "";
        text10.text = "";
    }

    private void Message(TextMeshPro target, string msg, float? y = null) {
        ClearText();
        target.text = msg;
        target.color = message;
        target.transform.localPosition = y.HasValue ? new Vector2(defaultLoc[target].x, y.Value) : defaultLoc[target];
    }

    private void Prompt(TextMeshPro target, string msg, float? y = null) {
        ClearText();
        target.text = msg;
        target.color = prompt;
        target.transform.localPosition = y.HasValue ? new Vector2(defaultLoc[target].x, y.Value) : defaultLoc[target];
    }

    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;

    private IEnumerator RunTutorial(Action cb) {
        IEnumerator wait(Func<bool> cond) {
            while (!cond()) yield return null;
        }
        IEnumerator confirm() {
            yield return null;
            yield return wait(() => UIConfirm.Active && EngineStateManager.State == EngineState.RUN);
        }
        ServiceLocator.Find<IUIManager>()
            .SetSpellname("Reduced Tutorial (For Players Too Smart for the Normal Tutorial)");

        var mov = new Movement(new Vector2(-2, 2.5f), 0);
        BulletManager.RequestSimple("lcircle-red/", _ => 4f, null, mov, new ParametricInfo(in mov));
        var nrx = new RealizedLaserOptions(new LaserOptions(), GenCtx.New(this, V2RV2.Zero), FiringCtx.New(), new Vector2(3, 5),
            V2RV2.Angle(-90), Cancellable.Null);
        "sync _ <> relrect greenrect level <-2;2.5:1.4;1.4:0> witha 0.7 green".Into<StateMachine>()
            .Start(new SMHandoff(this, Cancellable.Null));
        Message(text10, mtcirc1(UIConfirm.Desc));
        yield return confirm();
        BulletManager.ClearAllBullets();
        BehaviorEntity.GetExecForID("greenrect").InvokeCull();
        mov = new Movement(new Vector2(-3, 5), -90);
        BulletManager.RequestLaser(null, "mulaser-blue/b", mov, new ParametricInfo(in mov), 999, 0,
            ref nrx);
        mov = new Movement(new Vector2(-2, 5), -90);
        BulletManager.RequestLaser(null, "zonelaser-green/b", mov, new ParametricInfo(in mov), 999, 0,
            ref nrx);
        Message(text10, mtsafe2(UIConfirm.Desc));
        yield return confirm();
        cb();
    }
}
}
