using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
    private static readonly ExFunction quatEuler = Wrap<Quaternion, float>("Euler", 3);
    private static readonly ExFunction quatEuler3 = Wrap<Quaternion, Vector3>("Euler", 1);
    private static readonly Type[] noTypes = new Type[0];

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

    public static ExFunction Wrap(Type cls, string methodName, params Type[] types) {
        foreach (var mi in cls.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                        BindingFlags.Static)) {
            if (mi.Name.Equals(methodName)) {
                var mtypes = mi.GetParameters().Select(x => x.ParameterType).ToArray();
                if (mtypes.Length == types.Length) {
                    for (int ii = 0; ii < mtypes.Length; ++ii) {
                        if (mtypes[ii] != types[ii]) goto Next;
                    }
                    return new ExFunction(mi);
                }
                Next: ;
            }
        }
        throw new NotImplementedException(
            $"STATIC ERROR: Method {cls.Name}.{methodName} not found.");
    }

    public static ExFunction Wrap<C>(string methodName, params Type[] types) {
        return Wrap(typeof(C), methodName, types);
    }

    public static ExFunction Wrap<C>(string methodName) {
        return Wrap<C>(methodName, noTypes);
    }

    public static ExFunction Wrap<C, T>(string methodName, int typeCt = 1) {
        return Wrap<T>(typeof(C), methodName, typeCt);
    }

    public static ExFunction Wrap<T>(Type cls, string methodName, int typeCt = 1) {
        Type[] types = new Type[typeCt];
        Type ts = typeof(T);
        for (int ii = 0; ii < typeCt; ++ii) {
            types[ii] = ts;
        }
        return Wrap(cls, methodName, types);
    }

    public static ParameterExpression VFloat() => Ex.Variable(tfloat);
    public static ParameterExpression V<T>(string? name = null) => V(typeof(T), name);
    public static ParameterExpression V(Type t, string? name = null) => 
        name == null ? Ex.Variable(t) : Ex.Variable(t, name);

    //See notes for why these exist
    public static Ex AddAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Add(into, from));
    public static Ex SubAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Subtract(into, from));

    public static Ex MulAssign(Ex into, Ex from) => Ex.Assign(into, Ex.Multiply(into, from));
    
    private static readonly Dictionary<Type, ExFunction> containsKeyMIMap =
        new Dictionary<Type, ExFunction>();

    
    public static Ex DictContains(Ex dict, Ex key) {
        if (!containsKeyMIMap.TryGetValue(dict.Type, out ExFunction method)) {
            containsKeyMIMap[dict.Type] = method = 
                ExUtils.Wrap(dict.Type, "ContainsKey", key.Type);
        }
        return method.InstanceOf(dict, key);
    }

}

public class ExFunction {
    private readonly MethodInfo mi;

    public ExFunction(MethodInfo mi) {
        this.mi = mi;
    }

    public Ex Of(params Ex[] exs) {
        return Ex.Call(null, mi, exs);
    }

    public Ex InstanceOf(Ex instance, params Ex[] exs) {
        return Ex.Call(instance, mi, exs);
    }
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




public static class ExExtensions {
    public static Ex Eq(this Ex me, Ex other) => Ex.Equal(me, other);
    public static Ex Is(this Ex me, Ex other) => Ex.Assign(me, other);

    public static Ex Add(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 + f2) :
            Ex.Add(me, other);

    public static Ex Add(this Ex me, float other) => me.Add(Ex.Constant(other));

    public static Ex Sub(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 - f2) :
            Ex.Subtract(me, other);

    public static Ex Sub(this Ex me, float other) => me.Sub(Ex.Constant(other));

    public static Ex Mul(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 * f2) :
            Ex.Multiply(me, other);

    public static Ex Mul(this Ex me, float other) => me.Mul(Ex.Constant(other));

    public static Ex Div(this Ex me, Ex other) =>
        (me.TryAsConst(out float f1) && other.TryAsConst(out float f2)) ?
            (Ex) Ex.Constant(f1 / f2) :
            Ex.Divide(me, other);

    public static Ex Div(this Ex me, float other) => me.Div(Ex.Constant(other));
    public static Ex Neg(this Ex me) => Ex.Negate(me);
    public static Ex Complement(this Ex me) => Ex.Constant(1f).Sub(me);

    public static Ex Length(this Ex me) => Ex.ArrayLength(me);

    public static Ex Index(this Ex me, Ex index) => Ex.ArrayAccess(me, index);

    public static Ex Ipp(this Ex me) => Ex.PostIncrementAssign(me);

    public static Ex LT(this Ex me, Ex than) => Ex.LessThan(me, than);
    public static Ex LT0(this Ex me) => Ex.LessThan(me, Ex.Constant(0f));
    public static Ex GT(this Ex me, Ex than) => Ex.GreaterThan(me, than);
    public static Ex GT0(this Ex me) => Ex.GreaterThan(me, Ex.Constant(0f));
    public static Ex And(this Ex me, Ex other) => Ex.AndAlso(me, other);
    public static Ex Or(this Ex me, Ex other) => Ex.OrElse(me, other);

    public static Ex Field(this Ex me, string field) => Ex.PropertyOrField(me, field);

    public static Ex As<T>(this Ex me) => Ex.Convert(me, typeof(T));

    public static Ex DictGetOrThrow(this Ex dict, Ex key, string err) {
        return Ex.Block(
            Ex.IfThen(Ex.Not(ExUtils.DictContains(dict, key)), Ex.Throw(Ex.Constant(new Exception(err)))),
            dict.DictGet(key)
        );
        /*
        return Ex.Condition(ExUtils.DictContains<K, V>(dict, key), dict.DictGet(key), Ex.Block(
            Ex.Throw(Ex.Constant(new Exception(err))),
            dict.DictGet(key)
        ));*/
    }

    public static Ex DictSafeGet(this Ex dict, Ex key, Ex deflt) =>
        Ex.Condition(ExUtils.DictContains(dict, key), dict.DictGet(key), deflt);
    public static Ex DictGet(this Ex dict, Ex key) => Ex.Property(dict, "Item", key);
    public static Ex DictSet(this Ex dict, Ex key, Ex value) => Ex.Assign(Ex.Property(dict, "Item", key), value);

    public static bool IsConstant(this Ex ex) => ex.NodeType == ExpressionType.Constant;
    public static bool IsSimplifiable(this Ex ex) => ex.IsConstant() || ex.NodeType == ExpressionType.Parameter;

    public static bool TryAsConst<T>(this Ex ex, out T val) {
        if (ex is ConstantExpression cx && ex.Type == typeof(T)) {
            val = (T) cx.Value;
            return true;
        }
        val = default!;
        return false;
    }

    public static bool TryAsAnyConst(this Ex ex, out object? val) {
        if (ex is ConstantExpression cx && ex.Type.IsValueType) {
            val = cx.Value;
            return true;
        }
        val = default!;
        return false;
    }
}
}