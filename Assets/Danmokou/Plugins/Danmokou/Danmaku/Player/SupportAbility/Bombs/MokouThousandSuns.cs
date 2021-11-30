using System;
using System.Collections;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Player {
public partial record Ability {
    public record MokouThousandSuns : Bomb {
        protected override IEnumerator Execute(PlayerController bomber, IDisposable bombDisabler) {
            Logs.Log("Starting Mokou Thousand Suns bomb");
            var fireDisable = DisableFire(bomber);
        
            bomber.MakeInvulnerable(780, true);
            ServiceLocator.SFXService.Request("mokou-thousandsuns");
            SpawnCutin();
            BulletManager.SoftScreenClear();
            var task = bomber.RunExternalSM(SMRunner.RunRoot(SM!, bomber.BoundingToken), cancelOnFinish: true);
            for (int f = 0; f < 600; ++f) {
                if (f == 500) {
                    BulletManager.SoftScreenClear();
                }
                yield return null;
            }
            fireDisable.Dispose();
            while (!task.IsCompleted)
                yield return null;
            Logs.Log("Ending Mokou Thousand Suns bomb");
            bombDisabler.Dispose();
        }
    }
}
}