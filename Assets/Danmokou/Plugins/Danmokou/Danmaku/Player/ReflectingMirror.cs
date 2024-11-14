using System;
using System.Collections;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Services.GameManagement;

namespace Danmokou.Player {
public class ReflectingMirror : CoroutineRegularUpdater, IPlayerShotElement {
    private Flow stateFlow = null!;
    private PlayerController player = null!;
    public SpriteRenderer display = null!;

    public SFXConfig? onParry;
    public SFXConfig? onPerfectParry;

    private float timeInActiveState;
    private float timeSinceShieldActive;
    private Evented<Vector2Int> targetCoord = new(Vector2Int.zero) { AllowSameValueUpdate = false };
    private PlayerTargetingGridDisplay grid = null!;
    private float DifficultyFactor => Mathf.Pow(Instance.Difficulty.ValueRelLunatic, 0.2f);
    public float meterRegain = 0.01f;
    public float MeterRegain => meterRegain / DifficultyFactor;
    public float perfectParryWindow = 0.1f;
    public float shieldDropYieldTime = 0.05f;
    public Vector2 parryMeterRegain = new(0.01f, 0.05f);
    public Vector2 ParryMeterRegain => parryMeterRegain / DifficultyFactor;
    public float cooldownTime = 0.1f;
    public float instantMeterCost = 0.1f;
    public float InstantMeterCost => instantMeterCost * DifficultyFactor;
    public float meterCostStartsAfter = 0.1f;
    public float meterCostPerSecond = 0.35f;
    public float MeterCostPerSecond => meterCostPerSecond * DifficultyFactor;
    public float freezeFrameTime = 0.02f;
    public float parryVelocity = 6f;
    public Vector2Int parryScore = new(40, 200);
    
    
    private void Awake() {
        stateFlow = new(this);
    }

    public void Initialize(PlayerController playr) {
        this.player = playr;
        tokens.Add(player.CollisionChecker.AddConst((pc, typ, bpi) => this.CheckCollision(typ, bpi)));
        tokens.Add(player.GrazeChecker.AddConst((pc, typ, bpi) => this.CheckGraze(typ, bpi)));
        player.RunDroppableRIEnumerator(player.ShowMeterDisplay(null, Cancellable.Null, 0.25f));
    }

    private bool CheckCollision(BulletCollisionType typ, ParametricInfo bpi) {
        return CheckGraze(typ, bpi) is null;
    }

    private int? CheckGraze(BulletCollisionType typ, ParametricInfo bpi) {
        if (typ != BulletCollisionType.SimpleBullet) return null;
        if (!bpi.ctx.envFrame.MaybeGetValue<bool>("reflected").Valid) return null;
        ref var isRefl = ref bpi.ctx.envFrame.Value<bool>("reflected");
        var doReflect = stateFlow.State is State.Active || 
                        timeSinceShieldActive < shieldDropYieldTime ||
                        (timeInActiveState + timeSinceShieldActive) < (meterCostStartsAfter + shieldDropYieldTime);
        if (!doReflect)
            return isRefl ? 0 : null;
        if (!isRefl) {
            isRefl = true;
            var dir = (grid.GetLocation(targetCoord) - bpi.LocV2).normalized;
            bpi.ctx.envFrame.Value<Vector2>("vel") = dir * parryVelocity;
            var isPerfect = (stateFlow.State is State.Active && timeInActiveState < perfectParryWindow);
            ISFXService.SFXService.Request(isPerfect ? onPerfectParry : onParry);
            Instance.MeterF.AddMeter(isPerfect ? ParryMeterRegain.y : ParryMeterRegain.x);
            Instance.ScoreF.AddScore(isPerfect ? parryScore.y : parryScore.x);
            if (isPerfect)
                Counter.GrazeProc(20); //purely visual
            if (isPerfect && freezeFrameTime > 0f) 
                ServiceLocator.Find<FreezeFrameHelper>().CreateFreezeFrame(freezeFrameTime);
            return 1;
        }
        return 0;
    }

    public override void FirstFrame() {
        base.FirstFrame();
        grid = ServiceLocator.Find<PlayerTargetingGridDisplay>();
        Listen(targetCoord, x => grid.SelectEntry(x));
        stateFlow.Start();
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        var mov = GetMov();
        if (mov == Vector2Int.zero) return;
        var nxtCoord = targetCoord + mov;
        if (!grid.HasCoord(nxtCoord)) return;
        targetCoord.OnNext(nxtCoord);
    }

    private Vector2Int GetMov() {
        if (!player.AllowPlayerInput) goto end;
        if (InputManager.GetKeyTrigger(KeyCode.RightArrow).Active)
            return Vector2Int.right;
        if (InputManager.GetKeyTrigger(KeyCode.UpArrow).Active)
            return Vector2Int.up;
        if (InputManager.GetKeyTrigger(KeyCode.LeftArrow).Active)
            return Vector2Int.left;
        if (InputManager.GetKeyTrigger(KeyCode.DownArrow).Active)
            return Vector2Int.down;
        end: ;
        return Vector2Int.zero;
    }


    public record State {
        public record NULL : State;
        public record Inactive : State;
        public record Active(IDisposable meterToken) : State;
        public record Cooldown : State;
    }

    public class Flow : StateFlow<State> {
        private readonly ReflectingMirror src;

        public Flow(ReflectingMirror src) : base(new State.Inactive()) {
            this.src = src;
        }
        
        protected override void RunState(Maybe<State> prev) {
            if (State is State.NULL)
                return;
            src.RunRIEnumerator(State switch {
                State.Inactive => src.UpdateInactive(),
                State.Active act => src.UpdateActive(act.meterToken),
                State.Cooldown => src.UpdateCD(),
                _ => throw new Exception($"Unhandled camera state: {State}")
            });
        }
    }

    private bool IsFiringInput => player.AllowPlayerInput && player.FiringEnabled &&
                                  InputManager.GetKeyHold(KeyCode.Space).Active;

    private IEnumerator UpdateInactive() {
        display.enabled = false;
        for (; !stateFlow.GoToNextIfCancelled(); timeSinceShieldActive += ETime.dT) {
            if (IsFiringInput)
                if (Instance.MeterF.TryStartMeter(InstantMeterCost) is {} cT) {
                    //todo add meter requirement
                    stateFlow.GoToNext(new State.Active(cT));
                    yield break;
                } else
                    PlayerController.PlayerMeterFailed.OnNext(default);
            Instance.MeterF.AddMeter(MeterRegain * ETime.FRAME_TIME);
            yield return null;
        }
    }

    private IEnumerator UpdateActive(IDisposable meterToken) {
        display.enabled = true;
        timeInActiveState = 0;
        timeSinceShieldActive = 0;
        PlayerController.PlayerActivatedMeter.OnNext(default);
        player.speedLines.Play();
        for (int f = 0; !stateFlow.Cancelled && IsFiringInput && GameManagement.Instance.MeterF.TryUseMeterFrame( timeInActiveState < meterCostStartsAfter ? 0 : MeterCostPerSecond); ++f, timeInActiveState += ETime.dT) {
            PlayerController.MeterIsActive.OnNext(Instance.MeterF.EnoughMeterToUse ? player.meterDisplay : player.meterDisplayInner);
            yield return null;
        }
        meterToken.Dispose();
        player.speedLines.Stop();
        PlayerController.PlayerDeactivatedMeter.OnNext(default);
        stateFlow.GoToNextWithDefault(new State.Cooldown());
    }

    private IEnumerator UpdateCD() {
        display.enabled = false;
        for (var t = 0f; t < cooldownTime && !stateFlow.Cancelled; t += ETime.dT, timeSinceShieldActive += ETime.dT) {
            yield return null;
        }
        if (!stateFlow.GoToNextIfCancelled())
            stateFlow.GoToNext(new State.Inactive());
    }
    
    protected override void OnDisable() {
        stateFlow.SetNext(new State.NULL());
        base.OnDisable();
    }
}

}