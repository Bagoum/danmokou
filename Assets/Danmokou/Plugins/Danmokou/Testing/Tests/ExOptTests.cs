using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using UnityEngine;
using NUnit.Framework;
using Ex = System.Linq.Expressions.Expression;
using Danmokou.Expressions;
using static Danmokou.Expressions.ExMHelpers;
using static NUnit.Framework.Assert;

namespace Danmokou.Testing {

public class ExOptTests {
    
    [SetUp]
    public void Setup() {
        
    }
    private static float Compile(Ex ex) => Expression.Lambda<Func<float>>(ex).Compile()();
    private static T Compile<T>(Ex ex) => Expression.Lambda<Func<T>>(ex).Compile()();
    private static Func<float,float> Compile1(Ex ex, ParameterExpression pex) => Expression.Lambda<Func<float, float>>(ex, pex).Compile();
    private static float err = 0.00001f;

    private static ParameterExpression VF(string name) => Ex.Variable(typeof(float), name);
    private static ParameterExpression V<T>(string name) => Ex.Variable(typeof(T), name);

    public static float Add1(float x) => x + 1;

    [Test]
    public void TestFuncReplace() {
        Expression ex = Expression.Add(ExC(5f),
            Expression.Call(null, typeof(Mathf).GetMethod("Sin"), Ex.Multiply(E2, ExC(1.5f))));
        AreEqual(5.14112, Compile(ex), err);
        AreEqual("(5+Mathf.Sin(Null,(2*1.5)))", ex.Debug());
        AreEqual("5.14112", ex.FlatDebug());
        ex = Ex.Call(null, typeof(ExOptTests).GetMethod("Add1"), Ex.Multiply(E2, ExC(1.5f)));
        AreEqual("ExOptTests.Add1(Null,(2*1.5))", ex.Debug());
        AreEqual("ExOptTests.Add1(Null,3)", ex.FlatDebug());
    }

    [Test]
    public void TestConvert() {
        Ex ex = Ex.Add(ExC(5.2f), Ex.Convert(Ex.Convert(Ex.Add(ExC(1.2f), ExC(2.1f)), typeof(int)), typeof(float)));
        AreEqual("(5.2+(1.2+2.1):>Int32:>Single)", ex.Debug());
        AreEqual("8.2", ex.FlatDebug());
        var x = VF("x");
        ex = Ex.Add(ExC(5), Ex.Convert(Ex.Add(x, Ex.Add(ExC(1.2f), ExC(2.1f))), typeof(int)));
        AreEqual("(5+(x+(1.2+2.1)):>Int32)", ex.Debug());
        AreEqual("(5+(x+3.3):>Int32)", ex.FlatDebug());
    }

    [Test]
    public void TestIfThen() {
        var y = VF("y");
        var ex = Ex.Block(new[] {y}, Ex.Assign(y, ExC(4f)), Ex.Condition(Ex.GreaterThan(y, ExC(3f)),
            Ex.Assign(y, ExC(3f)),
            Ex.Add(y, ExC(1f))
        ), Ex.Add(y, ExC(2f)));
        AreEqual("((y=4);\nif(y>3){(y=3)}else{(y+1)};\n(y+2);)", ex.Debug());
        ex = Ex.Block(new[] {y}, Ex.Assign(y, ExC(4f)), Ex.Condition(Ex.GreaterThan(y, ExC(3f)),
            Ex.Add(y, ExC(1f)),
            Ex.Assign(y, ExC(3f))
        ), Ex.Add(y, ExC(2f)));
        AreEqual("((y=4);\n5;\n6;)", ex.FlatDebug());
    }

    [Test]
    public void TestReplaceWithVars() {
        var x = VF("x");
        Expression ex = Ex.Add(ExC(5f), Expression.Call(null, typeof(Mathf).GetMethod("Sin"), Ex.Add(x, Ex.Multiply(ExC(2f), ExC(1.5f)))));
        AreEqual("(5+Mathf.Sin(Null,(x+(2*1.5))))", ex.Debug());
        AreEqual("(5+Mathf.Sin(Null,(x+3)))", ex.FlatDebug());
        var y = VF("y");
        ex = Ex.Block(new[] {y}, Ex.Assign(y, ExC(3f)), Ex.Add(y, E2));
        AreEqual("((y=3);\n(y+2);)", ex.Debug());
        AreEqual("((y=3);\n5;)", ex.FlatDebug());
        ex = Ex.Block(new[] {y}, Ex.Assign(y, E1), Ex.Add(ExC(5f), Ex.Call(null, typeof(Mathf).GetMethod("Sin"), Ex.Add(y, Ex.Multiply(ExC(2f), ExC(1.5f))))));
        AreEqual("((y=1);\n(5+Mathf.Sin(Null,(y+(2*1.5))));)", ex.Debug());
        AreEqual("((y=1);\n4.243197;)", ex.FlatDebug());
        AreEqual(4.2431974, Compile(ex.Flatten()), err);
        ex = Ex.Block(new[] {y}, Ex.Assign(y, x), Ex.Add(ExC(5f), Expression.Call(null, typeof(Mathf).GetMethod("Sin"), Ex.Add(y, Ex.Multiply(E2, ExC(1.5f))))));
        AreEqual("((y=x);\n(5+Mathf.Sin(Null,(y+(2*1.5))));)", ex.Debug());
        AreEqual("((y=x);\n(5+Mathf.Sin(Null,(x+3)));)", ex.FlatDebug());
    }

    [Test]
    public void ReplaceMember() {
        var x = V<Vector2>("x");
        var y = VF("y");
        var ex = Ex.Block(new[] {x}, Ex.Assign(x, ExC(new Vector2(2f, 3f))), Ex.Field(x, "x").Add(ExC(6f)));
        AreEqual("((x=(2.0, 3.0));\n(x.x+6);)", ex.Debug());
        AreEqual("((x=(2.0, 3.0));\n8;)", ex.FlatDebug());
        AreEqual(Compile(ex.Flatten()), 8f);
        ex = Ex.Block(new[] {x}, Ex.Assign(x, ExC(new Vector2(2f, 3f))), Ex.IfThen(y.GT(E1), x.Is(ExC(Vector2.right))), Ex.Add(Ex.Field(x, "x"), ExC(6f)));
        AreEqual("((x=(2.0, 3.0));\nif(y>1){(x=(1.0, 0.0))}else{};\n(x.x+6);)", ex.FlatDebug());
        ex = Ex.Block(new[] {x}, x.Is(ExC(new Vector2(2f, 3f))), Ex.IfThen(y.GT(E1), Ex.Field(x, "x").Is(ExC(6f))), Ex.Add(Ex.Field(x, "x"), ExC(6f)));
        AreEqual("((x=(2.0, 3.0));\nif(y>1){(x.x=6)}else{};\n(x.x+6);)", ex.FlatDebug());
    }

    [Test]
    public void ZeroOneRemoval() {
        var x = VF("x");
        var ex = Ex.Block(Ex.Add(x, E1), Ex.Add(x, E0), E1.Mul(x), x.Mul(E0), x.Sub(E0), E0.Sub(x));
        AreEqual("((x+1);\n(x+0);\n(1*x);\n(x*0);\n(x-0);\n(0-x);)" ,ex.Debug());
        AreEqual("((x+1);\nx;\nx;\n0;\nx;\n(0-x);)" ,ex.FlatDebug());
    }
    
}
}