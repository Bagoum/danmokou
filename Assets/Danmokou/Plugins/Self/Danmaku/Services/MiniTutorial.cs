using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Danmaku;
using DMath;
using SM;
using TMPro;
using UnityEngine;
using static InputManager;
using static GameManagement;

public class MiniTutorial : BehaviorEntity {
    // Start is called before the first frame update
    public TextMeshPro text00;
    public TextMeshPro text10;
    public Color prompt;
    public Color message;
    private readonly Dictionary<TextMeshPro, Vector2> defaultLoc = new Dictionary<TextMeshPro, Vector2>();
    private static MiniTutorial main;
    protected override void Awake() {
        main = this;
        base.Awake();
        defaultLoc[text00] = text00.transform.localPosition;
        defaultLoc[text10] = text10.transform.localPosition;
    }

    public static void RunMiniTutorial(Action cb) => main.RunDroppableRIEnumerator(main.RunTutorial(cb));

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

    public override bool UpdateDuringPause => true;

    private IEnumerator RunTutorial(Action cb) {
        IEnumerator wait(Func<bool> cond) {
            while (!cond()) yield return null;
        }
        IEnumerator confirm() {
            yield return null;
            yield return wait(() => UIConfirm.Active && !GameStateManager.IsPaused);
        }
        DependencyInjection.Find<IUIManager>()
            .SetSpellname("Reduced Tutorial (For Players Too Smart for the Normal Tutorial)");
        
        BulletManager.RequestSimple("lcircle-red/", _ => 4f, null, new Velocity(new Vector2(-2, -2.5f), 0), 0, 0, null);
        var nrx = new RealizedLaserOptions(new LaserOptions(), GenCtx.New(this, V2RV2.Zero), 5, new Vector2(3, 5), V2RV2.Angle(-90), Cancellable.Null);
        "sync _ <> relrect greenrect level <-2;-2.5:1.4;1.4:0> witha 0.7 green".Into<StateMachine>()
            .Start(new SMHandoff(this, Cancellable.Null));
        Message(text10, $"You should see a large red circle on a green box in the bottom left corner. If the red circle is invisible or in the center of the screen, turn the legacy renderer option to YES in the pause menu. ({UIConfirm.Desc} to continue)");
        yield return confirm();
        BulletManager.ClearAllBullets();
        BehaviorEntity.GetExecForID("greenrect").InvokeCull();
        BulletManager.RequestLaser(null, "mulaser-blue/b", new Velocity(new Vector2(-3, 5), -90), 0, 5, 999, 0, ref nrx);
        BulletManager.RequestLaser(null, "zonelaser-green/b", new Velocity(new Vector2(-2, 5), -90), 0, 5, 999, 0, ref nrx);
        Message(text10, $"Lasers with letters or patterns are SAFE LASERS. They will not damage you. ({UIConfirm.Desc} to continue)");
        yield return confirm();
        cb();
    }

}
