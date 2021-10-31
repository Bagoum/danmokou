using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using Object = System.Object;

namespace Danmokou.Expressions {
public static class ExUtils {
    public static readonly Type tfloat = typeof(float);
    public static readonly Type tint = typeof(int);
    public static readonly Type tv2 = typeof(Vector2);
    public static readonly Type tv3 = typeof(Vector3);
    public static readonly Type tv4 = typeof(Vector4);
    public static readonly Type tvrv2 = typeof(V2RV2);
    private static readonly Type tqt = typeof(Quaternion);
    public static readonly Type tcc = typeof(CCircle);
    public static readonly Type tcr = typeof(CRect);
    private static readonly ConstructorInfo constrV2 = tv2.GetConstructor(new[] {tfloat, tfloat})!;
    private static readonly ConstructorInfo constrV3 = tv3.GetConstructor(new[] {tfloat, tfloat, tfloat})!;
    private static readonly ConstructorInfo constrV4 = tv4.GetConstructor(new[] {tfloat, tfloat, tfloat, tfloat})!;
    private static readonly ConstructorInfo constrRV2 =
        tvrv2.GetConstructor(new[] {tfloat, tfloat, tfloat, tfloat, tfloat})!;
    private static readonly ExFunction quatEuler = ExFunction.Wrap<Quaternion, float>("Euler", 3);
    private static readonly ExFunction quatEuler3 = ExFunction.Wrap<Quaternion, Vector3>("Euler", 1);

    public static Ex V2(Ex x, Ex y) {
        return Ex.New(constrV2, x, y);
    }

    public static Ex V3(Ex x, Ex y, Ex z) {
        return Ex.New(constrV3, x, y, z);
    }

    public static Ex V4(Ex x, Ex y, Ex z, Ex a) => Ex.New(constrV4, x, y, z, a);

    public static Ex VRV2(Ex nx, Ex ny, Ex rx, Ex ry, Ex angle) => Ex.New(constrRV2, nx, ny, rx, ry, angle);

    public static Ex QuaternionEuler(Ex x, Ex y, Ex z) {
        return quatEuler.Of(x, y, z);
    }

    public static Ex QuaternionEuler(Ex xyz) {
        return quatEuler3.Of(xyz);
    }

    public static Expression Property<T>(string property) => Property(typeof(T), property);

    public static Expression Property(Type t, string property) =>
        Expression.Property(null, t.GetProperty(property) ?? throw new Exception(
            $"STATIC EXCEPTION: Couldn't find property {property} on type {t}"));

    public static ParameterExpression VFloat() => Ex.Variable(tfloat);
    public static ParameterExpression V<T>(string? name = null) => V(typeof(T), name);
    public static ParameterExpression V(Type t, string? name = null) => 
        name == null ? Ex.Variable(t) : Ex.Variable(t, name);

    //See notes for why these exist
    public static Ex AddAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Add(into, from));
    public static Ex SubAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Subtract(into, from));

    public static Ex MulAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Multiply(into, from));

}


public enum ExMode {
    Parameter,
    RefParameter
}

/* Warnings for usage:
 -------
 Using a node twice will evaluate it twice. For example, consider this program:

public class Program {
	public static float Yeet(float f) {
		Console.WriteLine("yeet " + f);
		return f + 1;
	}
    public static void Main() {
		Expression fs = Ex.Constant(42f);
		Expression cexp = Ex.Call(null, typeof(Program).GetMethod("Yeet", new[] {typeof(float)}), fs);
		Ex exp = Ex.Block(Ex.Add(cexp, cexp));
		Console.WriteLine(Ex.Lambda<Func<float>>(exp).Compile()());
	}
}

This prints:
    yeet 42
    yeet 42
    86
-------
Do not modify BPI. Instead, copy it and modify the copy. This is because you are no longer using a recursive function, so modifications of BPI dirty it for the entire function tree.
Do not use MultiplyAssign/AddAssign/etc on struct fields. They are OK on raw values. Assignment is ok on struct fields. I wrote an alternate method, ExUtils.AddAssign/MulAssign, to fix this. Check this program:

public struct FloatWrapper {
	public float x;	
}

public class Program {
    public static void Main() {
        ParameterExpression f1 = Expression.Variable(typeof(FloatWrapper));
        ParameterExpression f2 = Expression.Variable(typeof(float));
		BlockExpression blockExpr = Expression.Block(
			new ParameterExpression[] { f1, f2 },
			Expression.Assign(Ex.Field(f1, "x"), Expression.Constant(5f)),
			Expression.Assign(f2, Expression.Constant(3f)),
			Expression.MultiplyAssign(Ex.Field(f1, "x"), Expression.Constant(100f)),
			Expression.MultiplyAssign(f2, Expression.Constant(1000f)),
			Ex.Add(Ex.Field(f1, "x"), f2)
		);
		Console.WriteLine(
			Expression.Lambda<Func<float>>(blockExpr).Compile()());
	}
}
It prints 3005, which shows that the mul-assign worked on the float but not on the field. 

*/
}