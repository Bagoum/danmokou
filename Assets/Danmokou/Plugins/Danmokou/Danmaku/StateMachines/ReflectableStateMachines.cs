using System.Threading.Tasks;
using System;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
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
/// `event`: Trigger events.
/// </summary>
[Reflect]
public class EventLASM : ReflectableLASM {
    public delegate Task Event(SMHandoff smh);
    
    public EventLASM(Event rs) : base(new TaskPattern(rs)) { }
    /// <summary>
    /// Make the player invulnerable for some number of frames.
    /// </summary>
    /// <param name="frames">Invulnerability frames (120 frames per second)</param>
    /// <returns></returns>
    public static Event PlayerInvuln(int frames) => smh => {
        PlayerHP.RequestPlayerInvulnerable.Publish((frames, true));
        DependencyInjection.SFXService.Request("x-invuln");
        return Task.CompletedTask;
    };

    public const float BossExplodeWait = 1.8f;
    private const float BossExplodeShake = 2.5f;
    private static readonly ReflWrap<FXY> ShakeMag = ReflWrap.FromFunc("BossExplode.ShakeMag",
        () => Compilers.FXY(b => ExMLerps.EQuad0m10(BossExplodeWait, BossExplodeShake, BPYRepo.T()(b))));

    public static Event BossExplode() => smh => {
        UnityEngine.Object.Instantiate(ResourceManager.GetSummonable("bossexplode")).GetComponent<ExplodeEffect>().Initialize(BossExplodeWait, smh.Exec.rBPI.loc);
        DependencyInjection.MaybeFind<IRaiko>()?.Shake(BossExplodeShake, ShakeMag, 2, smh.cT, null);
        DependencyInjection.SFXService.RequestSFXEvent(ISFXService.SFXEventType.BossExplode);
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