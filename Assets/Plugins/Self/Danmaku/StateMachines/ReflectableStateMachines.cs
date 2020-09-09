using System.Threading.Tasks;
using System;
using Danmaku;
using DMath;
using Core;
using JetBrains.Annotations;
using UnityEngine;

namespace SM {

public delegate Task TaskPattern(SMHandoff smh);
public delegate Task Synchronizer(SMHandoff smh);

public class ReflectableLASM : LineActionSM {
    private readonly TaskPattern func;
    public ReflectableLASM(TaskPattern func) {
        this.func = func;
    }
    public override Task Start(SMHandoff smh) => func(smh);
}

/// <summary>
/// `event`: Trigger events.
/// </summary>
public class EventLASM : ReflectableLASM {
    public EventLASM(TaskPattern rs) : base(rs) { }
    /// <summary>
    /// Make the player invulnerable for some number of frames.
    /// </summary>
    /// <param name="frames">Invulnerability frames (120 frames per second)</param>
    /// <returns></returns>
    public static TaskPattern PlayerInvuln(int frames) => smh => {
        Events.MakePlayerInvincible.Invoke(frames, true);
        return Task.CompletedTask;
    };

    [CanBeNull] private static FXY _shakeMag = null;
    private const float t = 1.8f;
    private const float st = 2.5f;
    private static FXY ShakeMag => _shakeMag = _shakeMag ?? Compilers.FXY(b => ExMLerps.EQuad0m10(t, st, FXYRepo.T()(b)));

    public static TaskPattern BossExplode() => smh => {
        UnityEngine.Object.Instantiate(ResourceManager.GetSummonable("bossexplode")).GetComponent<ExplodeEffect>().Initialize(t, smh.Exec.rBPI.loc);
        RaikoCamera.Shake(st, ShakeMag, 2, smh.cT, WaitingUtils.GetAwaiter(out Task _));
        SFXService.Request("x-boss-explode");
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, t, false);
    };

}

/// <summary>
/// `anim`: Run animations on the executing BEH.
/// </summary>
public class AnimatorControllerLASM : ReflectableLASM {
    public AnimatorControllerLASM(TaskPattern rs) : base(rs) {}

    /// <summary>
    /// Play the attack animation.
    /// </summary>
    /// <returns></returns>
    public static TaskPattern Attack() => smh => {
        smh.Exec.AnimateAttack();
        return Task.CompletedTask;
    };
}

/// <summary>
/// `timer`: Control shared timer objects.
/// </summary>
public class TimerControllerLASM : ReflectableLASM {
    public TimerControllerLASM(TaskPattern rs) : base(rs) {}

    /// <summary>
    /// Start a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    public static TaskPattern Start(ETime.Timer timer) => smh => {
        ETime.Timer.Start(timer);
        return Task.CompletedTask;
    };
    
    /// <summary>
    /// Restart a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    public static TaskPattern Restart(ETime.Timer timer) => smh => {
        ETime.Timer.Restart(timer);
        return Task.CompletedTask;
    };
    /// <summary>
    /// Start a timer with a speed multiplier.
    /// </summary>
    /// <param name="timer"></param>
    /// <param name="m"></param>
    /// <returns></returns>
    public static TaskPattern StartM(ETime.Timer timer, float m) => smh => {
        ETime.Timer.Start(timer, m);
        return Task.CompletedTask;
    };
    /// <summary>
    /// Pause a timer.
    /// </summary>
    /// <param name="timer"></param>
    /// <returns></returns>
    public static TaskPattern Stop(ETime.Timer timer) => smh => {
        ETime.Timer.Stop(timer);
        return Task.CompletedTask;
    };
}

}