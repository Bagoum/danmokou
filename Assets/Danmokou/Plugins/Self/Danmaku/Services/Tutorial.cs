using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.Danmaku.Options;
using DMK.DMath;
using DMK.GameInstance;
using DMK.Reflection;
using DMK.Scenes;
using DMK.SM;
using DMK.UI;
using TMPro;
using UnityEngine;
using static DMK.Core.InputManager;
using static DMK.Core.GameManagement;

namespace DMK.Services {
public class Tutorial : BehaviorEntity {
    // Start is called before the first frame update
    public TextMeshPro text00;
    public TextMeshPro text10;
    public Color prompt;
    public Color message;
    private readonly Dictionary<TextMeshPro, Vector2> defaultLoc = new Dictionary<TextMeshPro, Vector2>();
    public GameObject tutorialBoss;
    public TextAsset bossSM;

    protected override void Awake() {
        base.Awake();
#if UNITY_EDITOR
        RunDroppableRIEnumerator(RunTutorial(SKIP));
#else
        RunDroppableRIEnumerator(RunTutorial(0));
#endif
        defaultLoc[text00] = text00.transform.localPosition;
        defaultLoc[text10] = text10.transform.localPosition;
        GameManagement.instance.AddDecayRateMultiplier_Tutorial(6);
    }

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

    private IEnumerator RunTutorial(int skips) {
        while (SceneIntermediary.LOADING) yield return null;
        bool canSkip() => skips-- > 0;
        IEnumerator wait(Func<bool> cond) {
            if (!canSkip())
                while (!cond())
                    yield return null;
        }
        IEnumerator waitlf(Func<bool> cond) => wait(() => ETime.FirstUpdateForScreen && cond());
        IEnumerator waiti(InputHandler ih) {
            yield return null;
            yield return waitlf(() => ih.Active);
        }
        IEnumerator waitir(InputHandler ih) {
            yield return null;
            yield return waitlf(() => !ih.Active);
        }
        IEnumerator confirm() => waiti(UIConfirm);
        DependencyInjection.Find<IUIManager>().SetSpellname("Tutorial");
        Message(text10,
            $"Welcome to the tutorial! When you see a message in white, press {UIConfirm.Desc} to continue.");
        yield return confirm();
        Prompt(text10,
            $"When you see a message in blue, follow the instructions.\nPress {Pause.Desc} to open the pause menu.");
        yield return waitlf(() => EngineStateManager.IsPaused);
        UIManager.PauseMenu.GoToOption(0);
        Message(text00, $"The pause menu has important settings as well as control flow options.");
        yield return confirm();
        Message(text00, "If the game is running slow, you can try turning shaders off or lowering the resolution.");
        yield return confirm();
        Prompt(text00,
            "Shaders option ------------------>\nTry turning shaders on and off. It takes effect on unpause.", 2f);
        var sd = SaveData.s.Shaders;
        yield return waitlf(() => SaveData.s.Shaders != sd);
        Prompt(text00, "Resolution option ------------>\nTry changing the resolution. It takes effect immediately.",
            1.6f);
        var r = SaveData.s.Resolution;
        yield return waitlf(() => SaveData.s.Resolution != r);
        Message(text00,
            "Refresh rate option --------------->\nThis is the game speed. The engine will determine this automatically, but you can adjust it if the game is too fast or too slow.",
            0.4f);
        yield return confirm();
        Message(text00,
            "Fullscreen option --------------->\nSome computers have trouble playing games in fullscreen. Try turning this off if you have lag.",
            0.6f);
        yield return confirm();
        Message(text00,
            "Vsync option --------------->\nVsync will make the game run smoother, but it may cause input lag.", 0.2f);
        yield return confirm();
        Message(text00, "If you are sensitive to input lag, turn input smoothing off from the main menu options.",
            -0.7f);
        yield return confirm();
        Prompt(text00, $"Unpause by pressing {Pause.Desc} or selecting the unpause option.");
        yield return waitlf(() => !EngineStateManager.IsLoadingOrPaused);
        BulletManager.RequestSimple("lcircle-red/", _ => 4f, null, new Movement(new Vector2(-2, -2.5f), 0), 0, 0, null);
        var nrx = new RealizedLaserOptions(new LaserOptions(), GenCtx.New(this, V2RV2.Zero), 5, new Vector2(3, 5),
            V2RV2.Angle(-90), Cancellable.Null);
        BulletManager.RequestLaser(null, "mulaser-blue/b", new Movement(new Vector2(2, 5), -90), 0, 5, 999, 0, ref nrx);
        BulletManager.RequestLaser(null, "zonelaser-green/b", new Movement(new Vector2(3, 5), -90), 0, 5, 999, 0,
            ref nrx);
        "sync _ <> relrect greenrect level <-2;-2.5:1.4;1.4:0> witha 0.7 green".Into<StateMachine>()
            .Start(new SMHandoff(this));
        Message(text10,
            "You should now see a large red circle on a green box in the bottom left corner, and two lasers on the right side of the screen.");
        yield return confirm();
        Message(text10,
            "If you cannot see the red circle, or the red circle appears to be in the center of the screen, turn the legacy renderer option to YES in the pause menu.");
        yield return confirm();
        Message(text10, "The lasers on the right are SAFE LASERs. Lasers with letters or patterns do no damage.");
        yield return confirm();
        BulletManager.ClearAllBullets();
        BehaviorEntity.GetExecForID("greenrect").InvokeCull();

        Prompt(text10, $"Hold {ShootHold.Desc} to fire.");
        yield return waitir(ShootHold);
        yield return waiti(ShootHold);
        Prompt(text10, $"Use the arrow keys, the left joystick, or the D-Pad to move around.");
        yield return waitlf(() => Math.Abs(HorizontalSpeed01) > 0.1 || Math.Abs(VerticalSpeed01) > 0.1);
        Prompt(text10, $"Hold {FocusHold.Desc} to move slow (focus mode).");
        yield return waiti(FocusHold);

        var bcs = new Cancellable();
        var boss = GameObject.Instantiate(tutorialBoss).GetComponent<BehaviorEntity>();
        boss.Initialize(SMRunner.CullRoot(StateMachine.CreateFromDump(bossSM.text), bcs));
        IEnumerator phase() {
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
            var pct = boss.PhaseShifter;
            if (canSkip()) boss.ShiftPhase();
            else yield return wait(() => pct.Cancelled);
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
        }
        IEnumerator shift() {
            boss.ShiftPhase();
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
        }
        for (int ii = 0; ii < 8; ++ii) yield return null; //start delay

        Message(text10, "This is a boss enemy. The circle around the boss is its healthbar.");
        yield return confirm();
        Message(text10,
            "The white line at the bottom of the playable area changes into a colored boss healthbar when a boss is active.");
        yield return confirm();
        yield return shift();
        Prompt(text10,
            "The boss has started a nonspell card. Try shooting down the boss. You do up to 25% more damage when closer to the boss.");
        yield return phase();
        Prompt(text10,
            "This time, you can see two parts to the healthbar. The bottom half is the current nonspell card. The top half is the following spell card. Try shooting down the boss.");
        yield return phase();
        Prompt(text10,
            "The boss has started a spell card, using only the top half of the healthbar. Try shooting down the boss.");
        yield return phase();
        Prompt(text10,
            @"The boss has started a survival card. You cannot shoot down the boss. Wait for the timeout to the right of this text to hit zero.");
        yield return phase();
        Message(text10,
            "The amount of items dropped by the boss decreases gradually after 50% of the timeout has elapsed. Defeat the boss within the first 50% of the timeout for maximum rewards. (Does not apply to survival cards.)");
        yield return confirm();
        Message(text10, "Usually, bosses will fire bullets at you while you try to shoot them down.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Shoot down the boss, and try not to get hit.");
        yield return phase();

        Message(text10, @"These are your lives. ------->
A red dot is worth 2 lives,
a pink dot is worth 1 life.", 1.1f);
        yield return confirm();
        instance.SetLives(10);
        Message(text10, "There are 9 dots. Right now, you have 10 lives.");
        yield return confirm();
        instance.SetLives(15);
        Message(text10, "Now you have 15 lives.");
        yield return confirm();
        instance.SetLives(1);
        Message(text10, "Now you have 1 life.");
        yield return confirm();
        Message(text10, "If you are hit by bullets, you will lose a life. There are no bombs.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Try getting hit by the bullets.");
        yield return waitlf(() => EngineStateManager.IsDeath);
        Prompt(text00,
            "When you run out of lives, this screen will appear. Depending on the game mode, you may be able to continue. Select the continue option-- there's still more tutorial left!");
        yield return waitlf(() => !EngineStateManager.IsDeath);
        yield return shift();
        Message(text10, @"These are your life items. ---->
Fulfill the requirement to get an extra life.", 0.5f);
        yield return confirm();
        yield return shift();
        Prompt(text10,
            "Collect life items (red) by running into them. If you go above the point of collection, all items will move to you.");
        int currLives = instance.Lives;
        yield return waitlf(() => instance.Lives > currLives);
        yield return shift();
        Message(text10,
            $"Value items (blue) increase your score by {InstanceData.valueItemPoints}, with a bonus if you collect them higher on the screen or while using your special ability.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Get 75,000 points by collecting value items.");
        yield return waitlf(() => instance.Score > 75000);
        yield return shift();
        Message(text00, @"The score multiplier is the number below this text.
It multiplies the points gained from value items. Increase it by collecting point++ (green) items.");
        yield return confirm();
        Message(text00, $@"The faith meter is the white bar below the multiplier.
It will empty over time, but graze and point++ items will restore it. When empty, your multiplier will fall by {InstanceData.pivFallStep}.");
        yield return confirm();
        Message(text00,
            $@"While the faith meter is blue, it will not decay. Completing stage or boss sections, or collecting graze or point++ items, will add blue to the faith meter.");
        yield return confirm();
        Message(text10,
            "Grazing also increases the points gained from value items. This bonus is hidden, but does not decay.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Raise the score multiplier to 1.11 by collecting point++ items, then let it decay back to 1.");
        yield return waitlf(() => instance.PIV >= 1.11);
        yield return shift();
        yield return waitlf(() => instance.PIV <= 1.0);
        Message(text10, "If you get enough score, you will get extra lives. The first score extend is 2,000,000.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Get 2,000,000 points by collecting point++ items and value items.");
        yield return waitlf(() => instance.Score > 2000000);
        yield return shift();
        Message(text10, "There is a yellow bar below the decay meter, which allows you to use a special ability.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Hold X to activate bullet time, which slows the game speed to 50%.");
        GameManagement.instance.AddGems(100);
        yield return waitlf(() => InputManager.IsMeter);
        yield return shift();
        Message(text10,
            "While in bullet time, value items and point++ items are worth twice as much, and the player moves faster. Try collecting some items with or without bullet time.");
        yield return confirm();
        Message(text10, "You can refill the meter by collecting yellow gem items from defeated enemies.");
        yield return confirm();
        yield return shift();
        Message(text10, "In general, enemy bullets have much smaller hitboxes than their visual size.");
        yield return confirm();
        yield return shift();
        Message(text10, "The exception is sun bullets. These have very large hitboxes.");
        yield return confirm();
        yield return shift();
        Message(text10,
            "Also, safe lasers do not have hitboxes. Everything above the boss is a normal laser, and everything below is a safe laser.");
        yield return confirm();
        yield return shift();

        Prompt(text10, "That's all! To finish the tutorial, select \"Return to Menu\" from the pause menu.");
        SaveData.r.CompleteTutorial();
    }

    private const int SKIP = 10;
}
}
