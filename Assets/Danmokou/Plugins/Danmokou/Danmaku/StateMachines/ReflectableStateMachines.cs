using System.Threading.Tasks;
using System;
using System.Reactive;
using BagoumLib.Tasks;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;

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
    /// <param name="bindVar">Key to bind value of event on trigger</param>
    /// <param name="exec">StateMachine to execute with event</param>
    [GAlias(typeof(float), "listenf")]
    [GAlias(typeof(Unit), "listen0")]
    public static EventTask Listen<T>(string evName, string bindVar, StateMachine exec) => async smh => {
        using var _ = Events.FindRuntimeEvent<T>(evName).Ev.Subscribe(val => {
            var smh2 = new SMHandoff(smh, smh.ch, null);
            smh2.GCX.SetValue(bindVar, val);
            exec.Start(smh2).ContinueWithSync(smh2.Dispose);
        });
        await WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, 0f, true);
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
    [GAlias(typeof(float), "onnextf")]
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
        ServiceLocator.SFXService.Request("x-invuln");
        return Task.CompletedTask;
    };

    public const float BossExplodeWait = 1.8f;
    private const float BossExplodeShake = 2.5f;
    private static readonly ReflWrap<FXY> ShakeMag = ReflWrap.FromFunc("BossExplode.ShakeMag",
        () => Compilers.FXY(b => ExMLerps.EQuad0m10(BossExplodeWait, BossExplodeShake, BPYRepo.T()(b))));

    public static EventTask BossExplode() => smh => {
        UnityEngine.Object.Instantiate(ResourceManager.GetSummonable("bossexplode")).GetComponent<ExplodeEffect>().Initialize(BossExplodeWait, smh.Exec.rBPI.loc);
        ServiceLocator.MaybeFind<IRaiko>()?.Shake(BossExplodeShake, ShakeMag, 2, smh.cT, null);
        ServiceLocator.SFXService.RequestSFXEvent(ISFXService.SFXEventType.BossExplode);
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, BossExplodeWait, false);
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
        smh.Exec.AnimateAttack();
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
    public static TimerControl Start(ETime.Timer timer) => smh => {
        ETime.Timer.Start(timer);
        return Task.CompletedTask;
    };
    
    /// <summary>
    /// Restart a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
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
    public static TimerControl StartM(ETime.Timer timer, float m) => smh => {
        ETime.Timer.Start(timer, m);
        return Task.CompletedTask;
    };
    /// <summary>
    /// Pause a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    public static TimerControl Stop(ETime.Timer timer) => smh => {
        ETime.Timer.Stop(timer);
        return Task.CompletedTask;
    };
}

}