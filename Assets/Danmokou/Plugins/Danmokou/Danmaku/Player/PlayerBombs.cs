using System;
using System.Collections;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Reflection.Compilers;
using static Danmokou.DMath.Functions.ExMLerps;
using static Danmokou.DMath.Functions.ExMV4;
using Object = UnityEngine.Object;

namespace Danmokou.Player {
public enum PlayerBombType {
    NONE,
    TEST_BOMB_1,
    TEST_POWERBOMB_1,
    
    MimaBlackHole,
    MokouThousandSuns,
    ReimuFantasySeal
}

public enum PlayerBombContext {
    NORMAL,
    DEATHBOMB
}

public static partial class PlayerBombs {
    public static readonly Events.IEvent<(Bomb type, PlayerBombContext ctx)> BombFired =
        new Events.Event<(Bomb, PlayerBombContext)>();
    public static bool IsValid(this PlayerBombType bt) => bt != PlayerBombType.NONE;

    public static int DeathbombFrames(this PlayerBombType bt) =>
        bt switch {
            PlayerBombType.TEST_BOMB_1 => 10,
            PlayerBombType.TEST_POWERBOMB_1 => 10,
            PlayerBombType.MimaBlackHole => 10,
            PlayerBombType.MokouThousandSuns => 10,
            PlayerBombType.ReimuFantasySeal => 10,
            _ => 0
        };

    public static double? PowerRequired(this PlayerBombType bt) =>
        bt switch {
            PlayerBombType.TEST_POWERBOMB_1 => 1,
            _ => null
        };

    public static int? BombsRequired(this PlayerBombType bt) =>
        bt switch {
            PlayerBombType.TEST_BOMB_1 => 1,
            PlayerBombType.MimaBlackHole => 1,
            PlayerBombType.MokouThousandSuns => 1,
            PlayerBombType.ReimuFantasySeal => 1,
            _ => null
        };

    private static double ContextCostMultiplier(this PlayerBombType bt, PlayerBombContext ctx) =>
        bt switch {
            _ => 1
        };

    private static IEnumerator BombCoroutine(Bomb b, PlayerController bomber, MultiAdder.Token bombDisable) =>
        b.bomb switch {
            PlayerBombType.TEST_BOMB_1 => DoTestBomb1(bomber, bombDisable),
            PlayerBombType.TEST_POWERBOMB_1 => DoTestBomb1(bomber, bombDisable),
            PlayerBombType.MimaBlackHole => MimaBlackHoleBomb(b, bomber, bombDisable),
            PlayerBombType.MokouThousandSuns => MokouThousandSunsBomb(b, bomber, bombDisable),
            PlayerBombType.ReimuFantasySeal => ReimuFantasySealBomb(b, bomber, bombDisable),
            _ => throw new Exception($"No bomb handling for {b.bomb}")
        };

    public static bool TryBomb(Bomb b, PlayerController bomber, PlayerBombContext ctx) {
        var mult = b.bomb.ContextCostMultiplier(ctx);
        if (b.bomb.PowerRequired().Try(out var rp) && !GameManagement.Instance.TryConsumePower(-rp * mult))
            return false;
        if (b.bomb.BombsRequired().Try(out var rb) &&
            !GameManagement.Instance.TryConsumeBombs((int) Math.Round(-rb * mult)))
            return false;
        ++GameManagement.Instance.BombsUsed;
        BombFired.Publish((b, ctx));
        var ienum = BombCoroutine(b, bomber, PlayerController.BombDisabler.CreateToken1(MultiOp.Priority.CLEAR_SCENE));
        bomber.RunDroppableRIEnumerator(ienum);
        return true;
    }

    private static MultiAdder.Token DisableFire =>
        PlayerController.FiringDisabler.CreateToken1(MultiOp.Priority.CLEAR_SCENE);

    private static IEnumerator DoTestBomb1(PlayerController bomber, MultiAdder.Token bombDisable) {
        Log.Unity("Starting Test Bomb 1", level: Log.Level.DEBUG2);
        var fireDisable = DisableFire;
        var smh = new SMHandoff(bomber);
        //Note: you should use RunExternalSM, see the mokou bomb
        _ = TB1_1.Value(smh);
        _ = TB1_2.Value.Start(smh);
        PlayerController.RequestPlayerInvulnerable.Publish(((int) (120f * (EventLASM.BossExplodeWait + 3f)), true));
        for (float t = 0; t < EventLASM.BossExplodeWait; t += ETime.FRAME_TIME) yield return null;
        var circ = new CCircle(bomber.hitbox.location.x, bomber.hitbox.location.y, 8f);
        BulletManager.Autodelete(new SoftcullProperties(null, null),
            bpi => CollisionMath.PointInCircle(bpi.loc, circ));
        fireDisable.TryRevoke();
        for (float t = 0; t < 4f; t += ETime.FRAME_TIME) yield return null;
        Log.Unity("Ending Test Bomb 1", level: Log.Level.DEBUG2);
        bombDisable.TryRevoke();
    }

    private static IEnumerator MimaBlackHoleBomb(Bomb b, PlayerController bomber, MultiAdder.Token bombDisable) {
        Log.Unity("Starting Mima Black Hole bomb");
        var fireDisable = DisableFire;
        var bhe = new BlackHoleEffect(5, 0.5f, 1.5f);
        float totalTime = 7.5f;
        PlayerController.RequestPlayerInvulnerable.Publish(((int) (120f * totalTime), true));
        DependencyInjection.Find<IShaderCamera>().ShowBlackHole(bhe);
        DependencyInjection.Find<IRaiko>().Shake(3, null, 1);
        DependencyInjection.SFXService.Request("mima-blackhole");
        b.SpawnCutin();
        float t = 0;
        for (; t < bhe.absorbT; t += ETime.FRAME_TIME)
            yield return null;
        BulletManager.SoftScreenClear();
        var fe = Enemy.FrozenEnemies;
        for (int ii = 0; ii < fe.Count; ++ii) {
            if (fe[ii].Active) {
                fe[ii].enemy.QueuePlayerDamage(20000, 20000, bomber);
            }
        }
        fireDisable.TryRevoke();
        for (; t < totalTime; t += ETime.FRAME_TIME)
            yield return null;
        Log.Unity("Ending Mima Black Hole bomb");
        bombDisable.TryRevoke();
    }

    private static IEnumerator MokouThousandSunsBomb(Bomb b, PlayerController bomber, MultiAdder.Token bombDisable) {
        Log.Unity("Starting Mokou Thousand Suns bomb");
        var fireDisable = DisableFire;
        PlayerController.RequestPlayerInvulnerable.Publish((780, true));
        DependencyInjection.SFXService.Request("mokou-thousandsuns");
        b.SpawnCutin();
        BulletManager.SoftScreenClear();
        var task = bomber.RunExternalSM(SMRunner.RunNoCancelRoot(b.SM!), cancelOnFinish: true);
        for (int f = 0; f < 600; ++f) {
            if (f == 500) {
                BulletManager.SoftScreenClear();
            }
            yield return null;
        }
        fireDisable.TryRevoke();
        while (!task.IsCompleted)
            yield return null;
        Log.Unity("Ending Mokou Thousand Suns bomb");
        bombDisable.TryRevoke();
    }

    private static IEnumerator ReimuFantasySealBomb(Bomb b, PlayerController bomber, MultiAdder.Token bombDisable) {
        Log.Unity("Starting Reimu Fantasy Seal bomb");
        var fireDisable = DisableFire;
        PlayerController.RequestPlayerInvulnerable.Publish((900, true));
        b.SpawnCutin();
        var task = bomber.RunExternalSM(SMRunner.RunNoCancelRoot(b.SM!), cancelOnFinish: true);
        for (int ii = 0; ii < 600; ++ii) yield return null;
        fireDisable.TryRevoke();
        for (int ii = 0; ii < 180; ++ii) yield return null;
        //the task might run some more for waiting on controls
        Log.Unity("Ending Reimu Fantasy Seal bomb");
        bombDisable.TryRevoke();
    }
}

}