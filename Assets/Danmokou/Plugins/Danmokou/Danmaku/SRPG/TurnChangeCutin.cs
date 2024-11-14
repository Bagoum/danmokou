using System;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.SM;
using Scriptor.Compile;
using Scriptor.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.SRPG {
public class TurnChangeCutin : MonoBehaviour {
    private static readonly ReflWrap<Func<float, float, StateMachine>> MoveFactionText = 
        ReflWrap.FromFunc("TurnChangeAnimator.MoveFactionText", () => 
            CompileHelpers.ParseAndCompileDelegate<Func<float, float, StateMachine>>(@"
var aspect = 9.0/16.0;
var spd = 3.4;
var offset = -3.4 * spd;

saction 0 {
	pos(loc + pxy(isRL * offset, isUD * offset * aspect))
    move inf b{
		var t1 = lssht3(-4, 0.3, 4, 1.3, 10 * t, 0.2 * t, 9 * t)
		nroffset(spd * t1 * pxy(isRL, isUD * aspect))
	}
}
", new DelegateArg<float>("isRL"), new DelegateArg<float>("isUD")));
    
    public BehaviorEntity beh = null!;
    public SpriteRenderer bgPanel = null!;
    public TextMeshPro factionName = null!;
    public bool goesUp = true;

    public void Initialize(TurnChangeRequest req, Faction f) {
        bgPanel.color = f.Color.WithA(goesUp ? 1 : 0.4f);
        factionName.text = f.Name;
        Profiler.BeginSample("Get Mover");
        var mover = MoveFactionText.Value((req.NextOnLeft == (req.Next == f)) ? -1 : 1, goesUp ? 1 : -1);
        Profiler.EndSample();
        beh.RunExternalSM(SMRunner.Cull(mover, req.CT)).Log();
    }
}
}