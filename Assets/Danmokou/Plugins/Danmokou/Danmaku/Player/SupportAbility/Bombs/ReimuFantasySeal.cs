using System;
using System.Collections;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Player {
public partial record Ability {
    public record ReimuFantasySeal : Bomb {
        protected override IEnumerator Execute(PlayerController bomber, IDisposable bombDisabler) {
            Logs.Log("Starting Reimu Fantasy Seal bomb");
            var fireDisable = DisableFire(bomber);
            bomber.MakeInvulnerable(900, true);
            SpawnCutin();
            _ = bomber.RunExternalSM(SMRunner.RunRoot(SM!, bomber.BoundingToken), cancelOnFinish: true);
            for (int ii = 0; ii < 600; ++ii) yield return null;
            fireDisable.Dispose();
            for (int ii = 0; ii < 180; ++ii) yield return null;
            //the task might run some more for waiting on controls
            Logs.Log("Ending Reimu Fantasy Seal bomb");
            bombDisabler.Dispose();
        }
    }
}
}