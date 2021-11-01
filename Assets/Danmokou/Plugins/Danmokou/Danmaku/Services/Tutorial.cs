using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Scenes;
using Danmokou.SM;
using Danmokou.UI;
using Danmokou.UI.XML;
using TMPro;
using UnityEngine;
using static Danmokou.Core.InputManager;
using static Danmokou.Services.GameManagement;
using static Danmokou.Core.LocalizedStrings.Tutorial;

namespace Danmokou.Services {
public class Tutorial : BehaviorEntity {
    // Start is called before the first frame update
    public TextMeshPro text00 = null!;
    public TextMeshPro text10 = null!;
    public Color prompt;
    public Color message;
    private readonly Dictionary<TextMeshPro, Vector2> defaultLoc = new Dictionary<TextMeshPro, Vector2>();
    public GameObject tutorialBoss = null!;
    public TextAsset bossSM = null!;
    public int skip;

    protected override void Awake() {
        base.Awake();
#if UNITY_EDITOR
        RunDroppableRIEnumerator(RunTutorial(skip));
#else
        RunDroppableRIEnumerator(RunTutorial(0));
#endif
        defaultLoc[text00] = text00.transform.localPosition;
        defaultLoc[text10] = text10.transform.localPosition;
        tokens.Add(GameManagement.Instance.externalFaithDecayMultiplier.AddConst(6)); 
    }

    private void ClearText() {
        text00.text = "";
        text10.text = "";
    }

    private void Message(TextMeshPro target, string msg, float? y = null, float? x = null) {
        ClearText();
        target.text = msg;
        target.color = message;
        target.transform.localPosition =
            new Vector2(x ?? defaultLoc[target].x, y ?? defaultLoc[target].y);
    }

    private void Prompt(TextMeshPro target, string msg, float? y = null, float? x = null) {
        ClearText();
        target.text = msg;
        target.color = prompt;
        target.transform.localPosition =
            new Vector2(x ?? defaultLoc[target].x, y ?? defaultLoc[target].y);
    }

    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;

    private IEnumerator RunTutorial(int skips) {
        while (SceneIntermediary.LOADING) yield return null;
        bool canSkip() => skips-- > 0;
        IEnumerator wait(Func<bool> cond) {
            if (!canSkip())
                while (!cond())
                    yield return null;
        }
        IEnumerator waitlf(Func<bool> cond) => wait(() => ETime.FirstUpdateForScreen && cond());
        IEnumerator waiti(IInputHandler ih) {
            yield return null;
            yield return waitlf(() => ih.Active);
        }
        IEnumerator waitir(IInputHandler ih) {
            yield return null;
            yield return waitlf(() => !ih.Active);
        }
        IEnumerator confirm() => waiti(UIConfirm);
        ServiceLocator.Find<IUIManager>().SetSpellname("Tutorial");
        Message(text10, welcome1(UIConfirm.Desc));
        yield return confirm();
        Prompt(text10, blue2(Pause.Desc));
        yield return waitlf(() => EngineStateManager.State == EngineState.MENU_PAUSE);
        GameObject.FindObjectOfType<XMLPauseMenu>().GoToOption(0);
        const float menuLeft = -5f;
        Message(text00, pause3, x:menuLeft);
        yield return confirm();
        Message(text00, shaders4, x:menuLeft);
        yield return confirm();
        Prompt(text00, shaders5, 1.6f, x:menuLeft);
        var sd = SaveData.s.Shaders;
        yield return waitlf(() => SaveData.s.Shaders != sd);
        Prompt(text00, res6, 1.25f, x:menuLeft);
        var r = SaveData.s.Resolution;
        yield return waitlf(() => SaveData.s.Resolution != r);
        Message(text00, refresh7, 0.9f, x:menuLeft);
        yield return confirm();
        Message(text00, fullscreen8, 0.55f, x:menuLeft);
        yield return confirm();
        Message(text00, vsync9, 0.15f, x:menuLeft);
        yield return confirm();
        Message(text00, inputsmooth10, -0.7f, x:menuLeft);
        yield return confirm();
        Prompt(text00, unpause11(Pause.Desc), x:menuLeft);
        yield return waitlf(() => EngineStateManager.State == EngineState.RUN);
        var mov = new Movement(new Vector2(-2, 2.5f), 0);
        BulletManager.RequestSimple("lcircle-red/", _ => 4f, null, mov, new ParametricInfo(in mov));
        var nrx = new RealizedLaserOptions(new LaserOptions(), GenCtx.New(this, V2RV2.Zero), FiringCtx.New(), new Vector2(3, 5),
            V2RV2.Angle(-90), Cancellable.Null);
        mov = new Movement(new Vector2(2, 5), -90);
        BulletManager.RequestLaser(null, "mulaser-blue/b", mov, new ParametricInfo(in mov), 999, 0, ref nrx);
        mov = new Movement(new Vector2(3, 5), -90);
        BulletManager.RequestLaser(null, "zonelaser-green/b", mov, new ParametricInfo(in mov), 999, 0,
            ref nrx);
        "sync _ <> relrect greenrect level <-2;2.5:1.4;1.4:0> witha 0.7 green".Into<StateMachine>()
            .Start(new SMHandoff(this));
        Message(text10, redcircle12);
        yield return confirm();
        Message(text10, legacy13);
        yield return confirm();
        Message(text10, safelaser14);
        yield return confirm();
        BulletManager.ClearAllBullets();
        BehaviorEntity.GetExecForID("greenrect").InvokeCull();

        Prompt(text10, fire15(ShootHold.Desc));
        yield return waitir(ShootHold);
        yield return waiti(ShootHold);
        Prompt(text10, move16);
        yield return waitlf(() => Math.Abs(HorizontalSpeed01) > 0.1 || Math.Abs(VerticalSpeed01) > 0.1);
        Prompt(text10, focus17(FocusHold.Desc));
        yield return waiti(FocusHold);

        var bcs = new Cancellable();
        var boss = GameObject.Instantiate(tutorialBoss).GetComponent<BehaviorEntity>();
        boss.Initialize(SMRunner.CullRoot(StateMachine.CreateFromDump(bossSM.text), bcs));
        IEnumerator phase() {
            while (boss.PhaseShifter == null)
                yield return null;
            var pct = boss.PhaseShifter;
            if (canSkip()) boss.ShiftPhase();
            else yield return wait(() => pct.Cancelled);
            for (int ii = 0; ii < 244; ++ii) {
                yield return null; //phase delay
                if (EngineStateManager.State == EngineState.RUN) ++ii;
            }
        }
        IEnumerator shift() {
            boss.ShiftPhase();
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
        }
        for (int ii = 0; ii < 8; ++ii) yield return null; //start delay

        Message(text10, boss18);
        yield return confirm();
        Message(text10, hpbar19);
        yield return confirm();
        yield return shift();
        Prompt(text10, ns20);
        yield return phase();
        Prompt(text10, nss21);
        yield return phase();
        Prompt(text10, spell22);
        yield return phase();
        Prompt(text10, survival23);
        yield return phase();
        Message(text10, items24);
        yield return confirm();
        Message(text10, bullets25);
        yield return confirm();
        yield return shift();
        Prompt(text10, shoot26);
        yield return phase();

        Message(text10, lives27, 0.3f);
        yield return confirm();
        Instance.SetLives(10);
        Message(text10, dots28);
        yield return confirm();
        Instance.SetLives(15);
        Message(text10, dots29);
        yield return confirm();
        Instance.SetLives(1);
        Message(text10, dots30);
        yield return confirm();
        Message(text10, nobombs31);
        yield return confirm();
        yield return shift();
        Prompt(text10, pleasedie32);
        var dead = false;
        Listen(Instance.GameOver, _ => dead = true);
        yield return waitlf(() => dead);
        Prompt(text00, deathscreen33, x:menuLeft);
        yield return waitlf(() => EngineStateManager.State == EngineState.RUN);
        yield return shift();
        Message(text10, lifeitems34, -0.3f);
        yield return confirm();
        yield return shift();
        Prompt(text10, lifeitems35);
        int currLives = Instance.Lives;
        yield return waitlf(() => Instance.Lives > currLives);
        yield return shift();
        Message(text10, valueitems36(InstanceConsts.valueItemPoints));
        yield return confirm();
        yield return shift();
        Prompt(text10, points37);
        yield return waitlf(() => Instance.Score > 75000);
        yield return shift();
        Message(text00, scoremult38);
        yield return confirm();
        Message(text00, faith39(InstanceConsts.pivFallStep));
        yield return confirm();
        Message(text00, faithblue40);
        yield return confirm();
        Message(text10, graze41);
        yield return confirm();
        yield return shift();
        Prompt(text10, scoremult42);
        yield return waitlf(() => Instance.PIV >= 1.11);
        yield return shift();
        yield return waitlf(() => Instance.PIV <= 1.0);
        Message(text10, scoreext43);
        yield return confirm();
        yield return shift();
        Prompt(text10, scoreext44);
        yield return waitlf(() => Instance.Score > 2000000);
        yield return shift();
        Message(text10, ability45);
        yield return confirm();
        yield return shift();
        Prompt(text10, ability46(Meter.Desc));
        ServiceLocator.Find<PlayerController>().AddGems(100);
        yield return waitlf(() => InputManager.IsMeter);
        yield return shift();
        Message(text10, ability47);
        yield return confirm();
        Message(text10, meter48);
        yield return confirm();
        yield return shift();
        Message(text10, hitbox49);
        yield return confirm();
        yield return shift();
        Message(text10, hitbox50);
        yield return confirm();
        yield return shift();
        Message(text10, safelaser51);
        yield return confirm();
        yield return shift();

        Prompt(text10, end52);
        SaveData.r.CompleteTutorial();
    }
}
}
