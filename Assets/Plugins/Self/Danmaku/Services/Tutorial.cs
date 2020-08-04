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

public class Tutorial : BehaviorEntity {
    // Start is called before the first frame update
    public TextMeshPro text00;
    public TextMeshPro text10;
    public Color prompt;
    public Color message;
    private readonly Dictionary<TextMeshPro, Vector2> defaultLoc = new Dictionary<TextMeshPro, Vector2>();
    public GameObject tutorialBoss;
    public TextAsset bossSM;
    protected override void Start() {
        base.Start();
    #if UNITY_EDITOR
        RunDroppableRIEnumerator(RunTutorial(SKIP));
    #else
        RunDroppableRIEnumerator(RunTutorial(0));
    #endif
        defaultLoc[text00] = text00.transform.localPosition;
        defaultLoc[text10] = text10.transform.localPosition;
        GameManagement.campaign.AddDecayRateMultiplier_Tutorial(6);
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
        bool canSkip() => skips-- > 0;
        IEnumerator wait(Func<bool> cond) {
            if (!canSkip())
                while (!cond()) yield return null;
        }
        IEnumerator waitlf(Func<bool> cond) => wait(() => ETime.LastUpdateForScreen && cond());
        IEnumerator waiti(InputHandler ih) {
            yield return null;
            yield return waitlf(() => ih.Active);
        }
        IEnumerator waitir(InputHandler ih) {
            yield return null;
            yield return waitlf(() => !ih.Active);
        }
        IEnumerator confirm() => waiti(UIConfirm);
        UIManager.SetSpellname("Tutorial");
        Message(text10, $"Welcome to the tutorial! When you see a message in white, press {UIConfirm.Desc} to continue.");
        yield return confirm();
        Prompt(text10, $"When you see a message in blue, follow the instructions.\nPress {Pause.Desc} to open the pause menu.");
        yield return waitlf(() => GameStateManager.IsPaused);
        UIManager.PauseMenu.GoToOption(0);
        Message(text00, $"The pause menu has important settings as well as control flow options.");
        yield return confirm();
        Message(text00, "If the game is running slow, you can try turning shaders off or lowering the resolution.");
        yield return confirm();
        Prompt(text00, "Shaders option ------------------>\nTry turning shaders on and off. It takes effect on unpause.", 1.7f);
        var sd = SaveData.s.Shaders;
        yield return waitlf(() => SaveData.s.Shaders != sd);
        Prompt(text00, "Resolution option ------------------>\nTry changing the resolution. It takes effect immediately.", 1.3f);
        var r = SaveData.s.Resolution;
        yield return waitlf(() => SaveData.s.Resolution != r);
        Message(text00,"Refresh rate option --------------->\nThis is the game speed. The engine will determine this automatically, but you can adjust it if the game is too fast or too slow.", -0.1f);
        yield return confirm();
        Message(text00,"Fullscreen option --------------->\nSome computers have trouble playing games in fullscreen. Try turning this off if you have lag.", 0.2f);
        yield return confirm();
        Message(text00,"Vsync option --------------->\nVsync will make the game run smoother, but it may cause input lag.", -0.3f);
        yield return confirm();
        Message(text00,"Input smoothing option ---------->\nIf you are sensitive to input lag, turn this off.", -0.7f);
        yield return confirm();
        Prompt(text00, $"Unpause by pressing {Pause.Desc} or selecting the unpause option.");
        yield return waitlf(() => !GameStateManager.IsLoadingOrPaused);
        BulletManager.RequestSimple("lcircle-red/", _ => 4f, null, new Velocity(new Vector2(-3, -2.5f), 0), 0, 0, null);
        var nrx = new RealizedLaserOptions(new LaserOptions(LaserOption.S(_ => 1/RealizedLaserOptions.DEFAULT_LASER_WIDTH)), GenCtx.New(this, V2RV2.Zero), 5, new Vector2(3, 5), V2RV2.Angle(-90), MovementModifiers.Default, CancellationToken.None);
        BulletManager.RequestLaser(null, "mulaser-blue/b", new Velocity(new Vector2(3, 5), -90), 0, 5, 999, 0, ref nrx);
        BulletManager.RequestLaser(null, "zonelaser-green/b", new Velocity(new Vector2(4, 5), -90), 0, 5, 999, 0, ref nrx);
        "sync _ <> relrect greenrect level <-3,-2.5:1.4,1.4:0> witha 0.7 green".Into<StateMachine>()
            .Start(new SMHandoff(this, CancellationToken.None));
        Message(text10, "You should now see a large red circle on a green box in the bottom left corner, and two lasers on the right side of the screen.");
        yield return confirm();
        Message(text10, "If you cannot see the red circle, or the red circle appears to be in the center of the screen, turn the legacy renderer option to YES in the pause menu.");
        yield return confirm();
        Message(text10, "The lasers on the right are SAFE LASERs. Lasers with letters or patterns do no damage.");
        yield return confirm();
        BulletManager.ClearAllBullets();
        BehaviorEntity.GetExecForID("greenrect").InvokeCull();
        
        Prompt(text10, $"Hold {ShootHold.Desc} to fire.");
        yield return waitir(ShootHold);
        yield return waiti(ShootHold);
        Prompt(text10, $"Press {ShootToggle.Desc} to toggle firing on/off.");
        yield return waiti(ShootToggle);
        Prompt(text10, $"Press {AimLeft.Desc} while firing to aim left.");
        yield return waitlf(() => IsFiring && FiringDir == ShootDirection.LEFT);
        Prompt(text10, $"Press {AimRight.Desc} while firing to aim right.");
        yield return waitlf(() => IsFiring && FiringDir == ShootDirection.RIGHT);
        Prompt(text10, $"Press {AimUp.Desc} while firing to aim up.");
        yield return waitlf(() => IsFiring && FiringDir == ShootDirection.UP);
        Prompt(text10, $"Press {AimDown.Desc} while firing to aim down.");
        yield return waitlf(() => IsFiring && FiringDir == ShootDirection.DOWN);
        
        Prompt(text10, $"Use the arrow keys, the left joystick, or the D-Pad to move around.");
        yield return waitlf(() => Math.Abs(HorizontalSpeed) > 0.1 || Math.Abs(VerticalSpeed) > 0.1);
        Prompt(text10, $"Hold {FocusHold.Desc} to move slow (focus mode).");
        yield return waiti(FocusHold);
        
        var bcs = new CancellationTokenSource();
        var boss = GameObject.Instantiate(tutorialBoss).GetComponent<BehaviorEntity>();
        boss.Initialize(SMRunner.Cull(StateMachine.CreateFromDump(bossSM.text), bcs.Token));
        IEnumerator phase() {
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
            var pct = boss.PhaseShifter.Token;
            if (canSkip()) boss.ShiftPhase();
            else yield return wait(() => pct.IsCancellationRequested);
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
        }
        IEnumerator shift() {
            boss.ShiftPhase();
            for (int ii = 0; ii < 4; ++ii) yield return null; //phase delay
        }
        for (int ii = 0; ii < 8; ++ii) yield return null; //start delay
        
        Message(text10, "This is a boss enemy. The circle around the boss is its healthbar. The boss healthbar is duplicated in the bottom right for convenience.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "The boss has started a nonspell card. Try shooting down the boss. You do up to 20% more damage when closer to the boss.");
        yield return phase();
        Prompt(text10, "This time, you can see two parts to the healthbar. The bottom half is the current nonspell card. The top half is the following spell card. Try shooting down the boss.");
        yield return phase();
        Prompt(text10, "The boss has started a spell card, using only the top half of the healthbar. Try shooting down the boss.");
        yield return phase();
        Prompt(text10, @"The boss has started a timeout card. You cannot shoot down the boss. Wait for the timer to the right of this text to hit zero.");
        yield return phase();
        Message(text10, "Usually, bosses will fire bullets at you while you try to shoot them down.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Shoot down the boss, and try not to get hit.");
        yield return phase();

        Message(text10, @"These are your lives. --------------->
A red dot is worth 2 lives,
a pink dot is worth 1 life.", -1.5f);
        yield return confirm();
        campaign.SetLives(13);
        Message(text10, "There are 13 dots. Right now, you have 13 lives.");
        yield return confirm();
        campaign.SetLives(20);
        Message(text10, "Now you have 20 lives.");
        yield return confirm();
        campaign.SetLives(1);
        Message(text10, "Now you have 1 life.");
        yield return confirm();
        Message(text10, "If you are hit by bullets, you will lose a life. There are no bombs.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Try getting hit by the bullets.");
        yield return waitlf(() => GameStateManager.IsDeath);
        Prompt(text00, "When you run out of lives, this screen will appear. Depending on the game mode, you may be able to continue. Select the continue option-- there's still more tutorial left!");
        yield return waitlf(() => !GameStateManager.IsDeath);
        yield return shift();
        Message(text10, @"These are your life items. -------->
Fulfill the requirement to get an extra life.", -2.1f);
        yield return confirm();
        yield return shift();
        Prompt(text10, "Collect life items (red) by running into them. If you go above the point of collection, all items will move to you.");
        int currLives = campaign.Lives;
        yield return waitlf(() => campaign.Lives > currLives);
        yield return shift();
        Message(text10, $"Value items (blue) increase your score by {CampaignData.valueItemPoints}.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Get 35,000 points by collecting value items.");
        yield return waitlf(() => campaign.score > 35000);
        yield return shift();
        Message(text10, @"This is the score multiplier. ------>
It multiplies the points gained from value items. Increase it by collecting point++ items.", -0.4f);
        yield return confirm();
        Message(text10, $@"This is the decay meter. ---------->
It will empty over time, but graze and point++ items will restore it. When empty, your multiplier will fall by {CampaignData.pivFallStep}.", -1.1f);
        yield return confirm();
        Message(text10, $@"While the decay meter is bright blue, it will not decay. Completing stage or boss sections, or collecting graze or point++ items, will add some bright blue to the decay meter.", -1.1f);
        yield return confirm();
        Message(text10, "Grazing also increases the points gained from value items. This bonus is hidden, but does not decay.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Raise the score multiplier to 1.11 by collecting point++ items (green), then let it decay back to 1.");
        yield return waitlf(() => campaign.PIV >= 1.11);
        yield return shift();
        yield return waitlf(() => campaign.PIV <= 1.0);
        Message(text10, "If you get enough score, you will get extra lives. The first score extend is 1,000,000.");
        yield return confirm();
        yield return shift();
        Prompt(text10, "Get 1,000,000 points by collecting point++ items and value items.");
        yield return waitlf(() => campaign.score > 1000000);
        yield return shift();
        Message(text10, "In general, enemy bullets have much smaller hitboxes than their visual size.");
        yield return confirm();
        yield return shift();
        Message(text10, "The exception is sun bullets. These have very large hitboxes.");
        yield return confirm();
        yield return shift();
        Message(text10, "Also, safe lasers do not have hitboxes. Everything above the boss is a normal laser, and everything below is a safe laser.");
        yield return confirm();
        yield return confirm();
        yield return shift();
        
        Prompt(text10, "That's all! To finish the tutorial, select \"Quit to Menu\" from the pause menu.");
        SaveData.r.TutorialDone = true;
        SaveData.SaveRecord();
    }
    
    private const int SKIP = 0;

}
