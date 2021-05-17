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
public static partial class PlayerBombs {
    
    private static readonly ReflWrap<TaskPattern> TB1_1 = ReflWrap.FromFunc("PlayerBombs.TB1_1", 
        () => SMReflection.dBossExplode(
            TP4(LerpT(_ => 0.5f, _ => 1.5f, _ => Red(),
                _ => new Vector4(1f, 1f, 1f, 0.9f))),
            TP4(_ => Red())
        ));
    private static readonly ReflWrap<StateMachine> TB1_2 = new ReflWrap<StateMachine>(@"
async gpather-red/w <-90> gcr3 20 1.6s <> {
    frv2 angle(randpm1 * rand 20 50)
} pather(0.5, 0.5, tpnrot(
	truerotatelerprate(lerpt(1.2, 1.7, 170, 0),
		rotify(cx 1),
		(LNearestEnemy - loc)) 
            * lerp3(0.0, 0.3, 1.1, 1.3, t, 14, 2, 17)), { 
	player(120, 800, 100, oh1-red)
	s(2)
})
");
}
}