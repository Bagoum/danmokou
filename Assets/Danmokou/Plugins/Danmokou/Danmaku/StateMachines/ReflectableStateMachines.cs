﻿using System.Threading.Tasks;
using System;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Tasks;
using Danmokou.Behavior.Display;
using Danmokou.Behavior.Functions;
using Danmokou.Behavior.Items;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;
using Scriptor;
using static Danmokou.Danmaku.BulletManager;
using static Danmokou.Core.Extensions;

namespace Danmokou.SM {

/// <summary>
/// SM-like invocation for non-text SMs.
/// </summary>
public delegate Task TaskPattern(SMHandoff smh);
/// <summary>
/// SM-like invocation for text SMs.
/// </summary>
public delegate Task TTaskPattern(SMHandoff smh);
public delegate Task Synchronizer(SMHandoff smh);

public class ReflectableLASM : LineActionSM {
    private readonly TaskPattern func;
    public ReflectableLASM(TaskPattern func) {
        this.func = func;
    }
    public override Task Start(SMHandoff smh) => func(smh);

    public static implicit operator ReflectableLASM(TaskPattern func) => new(func);
}

/// <summary>
/// `event`: Handle configuring, subscribing to, and running events.
/// </summary>
[Reflect]
public class EventLASM : ReflectableLASM {
    public delegate Task EventTask(SMHandoff smh);
    
    public EventLASM(EventTask rs) : base(new TaskPattern(rs)) { }
    
    /// <summary>
    /// Subscribe to a runtime event and run a StateMachine when a value is provided.
    /// </summary>
    /// <param name="evName">Runtime event name</param>
    /// <param name="exec">StateMachine to execute with event</param>
    [GAlias("listenf", typeof(float))]
    [GAlias("listen0", typeof(Unit))]
    public static EventTask Listen<T>(string evName, Func<T, StateMachine> exec) => async smh => {
        using var _ = Events.FindRuntimeEvent<T>(evName).Ev.Subscribe(val => {
            var smh2 = new SMHandoff(smh, smh.ch.Mirror(), null);
            exec(val).Start(smh).ContinueWithSync(smh2.Dispose);
        });
        await RUWaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, 0f, true);
        smh.ThrowIfCancelled();
    };

    /// <summary>
    /// Set a trigger event to reset when a given reset event is dispatched.
    /// </summary>
    public static EventTask ResetTrigger(string trigger, string resetter) => smh => {
        Events.FindAnyRuntimeEvent(trigger).TriggerResetWith(Events.FindAnyRuntimeEvent(resetter));
        return Task.CompletedTask;
    };
    
    
    /// <summary>
    /// Push a value to a runtime event.
    /// </summary>
    [GAlias("onnextf", typeof(float))]
    public static EventTask OnNext<T>(string evName, GCXF<T> val) => smh => {
        Events.ProcRuntimeEvent<T>(evName, val(smh.GCX));
        return Task.CompletedTask;
    };


    /// <summary>
    /// Push to a unit-typed runtime event.
    /// </summary>
    public static EventTask OnNext0(string evName) => OnNext(evName, _ => Unit.Default);
    
    /// <summary>
    /// Make the player invulnerable for some number of frames.
    /// </summary>
    /// <param name="frames">Invulnerability frames (120 frames per second)</param>
    /// <returns></returns>
    public static EventTask PlayerInvuln(int frames) => smh => {
        foreach (var player in ServiceLocator.FindAll<PlayerController>())
            player.MakeInvulnerable(frames, true);
        ISFXService.SFXService.Request("x-invuln");
        return Task.CompletedTask;
    };

    public const float BossExplodeWait = 1.8f;
    private const float BossExplodeShake = 2.5f;
    private static readonly ReflWrap<FXY> ShakeMag = ReflWrap.FromFunc("BossExplode.ShakeMag",
        () => Compilers.FXY(b => ExMLerps.EQuad0m10(BossExplodeWait, BossExplodeShake, AtomicBPYRepo.T()(b))));

    public static EventTask BossExplode() => smh => {
        var useCT = smh.Context.ExternalCT ?? smh.cT;
        UnityEngine.Object.Instantiate(ResourceManager.GetSummonable("bossexplode"))
            .GetComponent<ExplodeEffect>().Initialize(BossExplodeWait, smh.Exec.rBPI.loc, useCT, WaitingUtils.GetAwaiter(out var t));
        ServiceLocator.FindOrNull<IRaiko>()?.Shake(BossExplodeShake, ShakeMag, 2, useCT, null);
        ISFXService.SFXService.RequestSFXEvent(ISFXService.SFXEventType.BossExplode);
        return t;
    };

}

/// <summary>
/// `anim`: Run animations on the executing BEH.
/// </summary>
[Reflect]
public class AnimatorControllerLASM : ReflectableLASM {
    public delegate Task Animate(SMHandoff smh);
    
    public AnimatorControllerLASM(Animate rs) : base(new TaskPattern(rs)) {}

    /// <summary>
    /// Play the attack animation.
    /// </summary>
    /// <returns></returns>
    public static Animate Attack() => smh => {
        smh.Exec.Dependent<DisplayController>().Animate(AnimationType.Attack, false, null);
        return Task.CompletedTask;
    };
}

/// <summary>
/// `timer`: Control shared timer objects.
/// </summary>
[Reflect]
public class TimerControllerLASM : ReflectableLASM {
    public delegate Task TimerControl(SMHandoff smh);
    
    public TimerControllerLASM(TimerControl rs) : base(new TaskPattern(rs)) {}

    /// <summary>
    /// Start a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    [BDSL1Only]
    public static TimerControl Start(ETime.Timer timer) => smh => {
        ETime.Timer.Start(timer);
        return Task.CompletedTask;
    };
    
    /// <summary>
    /// Restart a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    [BDSL1Only]
    public static TimerControl Restart(ETime.Timer timer) => smh => {
        ETime.Timer.Restart(timer);
        return Task.CompletedTask;
    };
    /// <summary>
    /// Start a timer with a speed multiplier.
    /// </summary>
    /// <param name="timer"></param>
    /// <param name="m"></param>
    /// <returns></returns>
    [BDSL1Only]
    public static TimerControl StartM(ETime.Timer timer, float m) => smh => {
        ETime.Timer.Start(timer, m);
        return Task.CompletedTask;
    };
    /// <summary>
    /// Pause a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    [BDSL1Only]
    public static TimerControl Stop(ETime.Timer timer) => smh => {
        ETime.Timer.Stop(timer);
        return Task.CompletedTask;
    };
}

/// <summary>
/// `collide`: Set up collision handlers for bullet-on-bullet collision.
/// </summary>
[Reflect]
public class BxBCollideLASM : ReflectableLASM {
    public delegate Task ColliderFn(SMHandoff smh);
    public BxBCollideLASM(ColliderFn fn) : base(new TaskPattern(fn)) { }
    
    /// <summary>
    /// Set up collision handlers between simple bullet pools.
    /// <br/>A collision occurs if a bullet from the left pools overlaps a bullet from the right pools.
    /// </summary>
    /// <param name="left">Left pools</param>
    /// <param name="right">Right pools</param>
    /// <param name="leftPred">Predicate deciding whether a bullet in the left pools should test for collisions</param>
    /// <param name="rightPred">Predicate deciding whether a bullet in the right pools should test for collisions</param>
    /// <param name="leftCtrls">Controls to run on left bullets when they collide</param>
    /// <param name="rightCtrls">Controls to run on right bullets when they collide</param>
    /// <returns></returns>
    public static ColliderFn SBOnSB(StyleSelector left, StyleSelector right, Pred leftPred, Pred rightPred, cBulletControl[] leftCtrls,
        cBulletControl[] rightCtrls) => smh => {
        _ = new BxBCollisionSBOnSB(smh.cT, BulletManager.StylesForSelector(left), 
            BulletManager.StylesForSelector(right).ToList(), leftPred, rightPred, leftCtrls, rightCtrls);
        return Task.CompletedTask;
    };
}

}