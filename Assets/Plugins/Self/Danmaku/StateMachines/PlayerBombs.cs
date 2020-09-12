using System;
using System.Collections;
using System.Threading.Tasks;
using DMath;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static Compilers;
using static DMath.ExMLerps;
using static DMath.ExMV4;

namespace Danmaku {
public enum PlayerBombType {
    NONE,
    TEST_BOMB_1
}
public static class PlayerBombs {
    public static bool IsValid(this PlayerBombType bt) => bt != PlayerBombType.NONE;

    public static int DeathbombFrames(this PlayerBombType bt) {
        switch (bt) {
            case PlayerBombType.TEST_BOMB_1:
                return 20;
            default:
                return 0;
        }
    }
    
    public static void DoBomb(PlayerBombType bomb, PlayerInput bomber) {
        IEnumerator ienum;
        switch (bomb) {
            case PlayerBombType.TEST_BOMB_1:
                ienum = DoTestBomb1(bomber);
                break;
            default:
                throw new Exception($"No bomb handling for {bomb}");
        }
        ++PlayerInput.BombDisableRequests;
        bomber.RunDroppableRIEnumerator(ienum);
    }

    public static void DoDeathbomb(PlayerBombType bomb, PlayerInput bomber) => DoBomb(bomb, bomber);
    
    private static readonly ReflWrap<TaskPattern> TB1_1 = (Func<TaskPattern>)(() => SMReflection.dBossExplode(
        TP4(LerpT(_ => 0.5f, _ => 1.5f, _ => Red(),
            _ => new Vector4(1f, 1f, 1f, 0.9f))),
        TP4(_ => Red())
    ));
    private static readonly ReflWrap<StateMachine> TB1_2 = (Func<StateMachine>)@"
async p-gpather-red/w <-90> gcr3 20 1.6s <> {
    frv2 angle(randpm1 * rand 20 50)
} pather(0.5, 0.5, tpnrot(
	truerotatelerprate(lerpt(1.2, 1.7, 170, 0),
		rotify(cx 1),
		(LNearestEnemy - loc)) 
            * lerp3(0.0, 0.3, 1.1, 1.3, t, 14, 2, 17)), { 
	player(120, 800, 100, oh1)
	s(2)
})
".Into<StateMachine>;
    private static IEnumerator DoTestBomb1(PlayerInput bomber) {
        Log.Unity("Starting Test Bomb 1", level: Log.Level.DEBUG2);
        ++PlayerInput.FiringDisableRequests;
        var smh = new SMHandoff(bomber);
        _ = TB1_1.Value(smh);
        _ = TB1_2.Value.Start(smh);
        Core.Events.MakePlayerInvincible.Invoke(((int)(120f * (EventLASM.BossExplodeWait + 3f)), true));
        for (float t = 0; t < EventLASM.BossExplodeWait; t += ETime.FRAME_TIME) yield return null;
        var circ = new CCircle(bomber.hitbox.location.x, bomber.hitbox.location.y, 8f);
        BulletManager.Autodelete("cwheel", "black/b", bpi => DMath.Collision.PointInCircle(bpi.loc, circ));
        --PlayerInput.FiringDisableRequests;
        for (float t = 0; t < 4f; t += ETime.FRAME_TIME) yield return null;
        Log.Unity("Ending Test Bomb 1", level: Log.Level.DEBUG2);
        --PlayerInput.BombDisableRequests;
    }
    
}
}