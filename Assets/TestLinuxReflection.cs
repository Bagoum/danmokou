using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Reflection.CompilerHelpers;
using Ex = System.Linq.Expressions.Expression;

public class TestLinuxReflection : MonoBehaviour {
	private const string sel = @"select(powerIndex, { 
			0
			(30 * pm1(p))
			if(= p 2, 0, 30 * pm1(p))
			(30 * pm1(p) * if(> p 1, 0.6, 1))
		})";
	private readonly string ang = $"angle({sel})";

	private const string selS = @"select(&x, {
			0
			(30 * pm1(&y))
			if(= &y 2, 0, 30 * pm1(&y))
			(30 * pm1(&y) * if(> &y 1, 0.6, 1))
		})";
	private readonly string angS = $"angle({selS})";

	public static Ex ExC(object o) => Ex.Constant(o);

	public static Ex PM1(Ex x) => ExC(1f).Sub(ExC(2f).Mul(
		x.Sub(ExC(2f).Mul(Ex.Call(null, typeof(Math).GetMethod("Floor", new[]{typeof(double)})!,
			x.Div(ExC(2f)).Cast<double>()).Cast<float>()))));

	IEnumerator Start() {
	    GameManagement.Instance.PowerF.Power.OnNext(2);
	    var gcx = GenCtx.New(GetComponent<BehaviorEntity>());
	    gcx.index = 7;
	    var bpi = gcx.AsBPI;
        for (float t = 0; t < 1f; t += Time.deltaTime)
            yield return null;
        Debug.Log("Starting reductions");
        Debug.Log("powerIndex".Into<BPY>()(bpi));
        Debug.Log("index 0: " + "0".Into<BPY>()(bpi));
        Debug.Log("index 1: " + "(30 * pm1(p))".Into<BPY>()(bpi));
        Debug.Log("index 2: " + "if(= p 2, 0, 30 * pm1(p))".Into<BPY>()(bpi));
        Debug.Log("index 3: " + "(30 * pm1(p) * if(> p 1, 0.6, 1))".Into<BPY>()(bpi));
        Debug.Log("as bpi: " + sel.Into<BPY>()(bpi));
        Debug.Log("as gcx: " + sel.Into<GCXF<float>>()(gcx));

        var x = Ex.Parameter(typeof(float), "x");
        var y = Ex.Parameter(typeof(float), "y");
        var ex_ang =
	        Ex.Condition(x.Cast<int>().Eq(ExC(0)), 
		        ExC(0f),
		        Ex.Condition(x.Cast<int>().Eq(ExC(1)), 
			        PM1(y).Mul(ExC(30f)),
			        Ex.Condition(x.Cast<int>().Eq(ExC(2)), 
				        Ex.Condition(y.Eq(ExC(2f)), ExC(0f), PM1(y).Mul(ExC(30f))),
				        Ex.Condition(y.GT(ExC(1f)), ExC(0.6f), ExC(1f)).Mul(PM1(y).Mul(ExC(30f)))
			        )
		        )
	        );
        var ex_angrv2 = Ex.New(typeof(V2RV2).GetConstructor(new[]
		        { typeof(float), typeof(float), typeof(float), typeof(float), typeof(float) })!,
	        ExC(0f), ExC(0f), ExC(0f), ExC(0f), ex_ang);

        Debug.Log("as xy raw: " + new TEx<float>(ex_ang)
	        .BakeAndCompile<Func<float, float, float>>(null!, x, y)(2, 9));
        Debug.Log("as xyrv2 raw: " + new TEx<V2RV2>(ex_angrv2)
	        .BakeAndCompile<Func<float, float, V2RV2>>(null!, x, y)(2, 9));

        Debug.Log("as xy: " + CompileDelegateFromString<Func<float, float, float>>(selS,
	        new DelegateArg<float>("x"),
	        new DelegateArg<float>("y")
        )(2, 9));
        Debug.Log("as xyrv2: " + CompileDelegateFromString<Func<float, float, V2RV2>>(angS,
	        new DelegateArg<float>("x"),
	        new DelegateArg<float>("y")
        )(2, 9));
        Debug.Log("as bpirv2: " + ang.Into<BPRV2>()(bpi));
        Debug.Log("as gcxrv2: " + ang.Into<GCXF<V2RV2>>()(gcx));
    }
}