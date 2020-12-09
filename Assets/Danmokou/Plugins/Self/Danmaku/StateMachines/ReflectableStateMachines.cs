using System.Threading.Tasks;
using System;
using DMK.Behavior.Functions;
using DMK.Core;
using DMK.DMath;
using DMK.DMath.Functions;
using DMK.Player;
using DMK.Reflection;
using DMK.Services;

namespace DMK.SM {

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
        PlayerHP.RequestPlayerInvulnerable.Publish((frames, true));
        SFXService.Request("x-invuln");
        return Task.CompletedTask;
    };

    public const float BossExplodeWait = 1.8f;
    private const float BossExplodeShake = 2.5f;
    private static readonly ReflWrap<FXY> ShakeMag =(Func<FXY>)(() => Compilers.FXY(b => ExMLerps.EQuad0m10(BossExplodeWait, BossExplodeShake, FXYRepo.T()(b))));

    public static TaskPattern BossExplode() => smh => {
        UnityEngine.Object.Instantiate(ResourceManager.GetSummonable("bossexplode")).GetComponent<ExplodeEffect>().Initialize(BossExplodeWait, smh.Exec.rBPI.loc);
        DependencyInjection.MaybeFind<IRaiko>()?.Shake(BossExplodeShake, ShakeMag, 2, smh.cT, null);
        SFXService.BossExplode();
        return WaitingUtils.WaitForUnchecked(smh.Exec, smh.cT, BossExplodeWait, false);
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