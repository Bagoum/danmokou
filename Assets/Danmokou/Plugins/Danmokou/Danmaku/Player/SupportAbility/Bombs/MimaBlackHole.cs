using System;
using System.Collections;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Player {
public partial record Ability {
    public record MimaBlackHole : Bomb {
        protected override IEnumerator Execute(PlayerController bomber, IDisposable bombDisabler) {
            Logs.Log("Starting Mima Black Hole bomb");
            var fireDisable = DisableFire(bomber);
            var bhe = new BlackHoleEffect(5, 0.5f, 1.5f);
            float totalTime = 7.5f;
            bomber.MakeInvulnerable((int) (120f * totalTime), true);
            ServiceLocator.Find<IShaderCamera>().ShowBlackHole(bhe);
            ServiceLocator.Find<IRaiko>().Shake(3, null, 1);
            ISFXService.SFXService.Request("mima-blackhole");
            SpawnCutin();
            float t = 0;
            for (; t < bhe.absorbT; t += ETime.FRAME_TIME)
                yield return null;
            BulletManager.SoftScreenClear();
            var fe = Enemy.FrozenEnemies;
            for (int ii = 0; ii < fe.Count; ++ii)
                if (fe[ii].Active)
                    fe[ii].enemy.QueuePlayerDamage(8000, 8000, bomber);
            fireDisable.Dispose();
            for (; t < totalTime; t += ETime.FRAME_TIME)
                yield return null;
            Logs.Log("Ending Mima Black Hole bomb");
            bombDisabler.Dispose();
        }
    }
}
}