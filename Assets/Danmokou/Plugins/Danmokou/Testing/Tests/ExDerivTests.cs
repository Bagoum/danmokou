using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using NUnit.Framework;
using Ex = System.Linq.Expressions.Expression;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using static Danmokou.Expressions.ExMHelpers;
using static NUnit.Framework.Assert;
using static Danmokou.DMath.Functions.ExM;

namespace Danmokou.Testing {

public class ExDerivTests {
    private static float Compile(Ex ex) => Expression.Lambda<Func<float>>(ex).Compile()();
    private static T Compile<T>(Ex ex) => Expression.Lambda<Func<T>>(ex).Compile()();
    private static Func<float,float> Compile1(Ex ex, ParameterExpression pex) => Expression.Lambda<Func<float, float>>(ex, pex).Compile();

    private static ParameterExpression VF(string name) => Ex.Variable(typeof(float), name);
    private static ParameterExpression V<T>(string name) => Ex.Variable(typeof(T), name);

    private static string DerivDebug(Expression x, Ex ex) => ex.Derivate(x, E1).Flatten(false).Debug();

    [Test]
    public void TestCosSin() {
        var x = VF("x");
        AreEqual("((2*(-1*(M.SinDeg(Null,x)*57.29578)))*M.Cos(Null,(2*M.CosDeg(Null,x))))", DerivDebug(x, Sin(E2.Mul(CosDeg(x)))));
        AreEqual("(2*M.Sin(Null,((x*-2)+3)))", DerivDebug(x, E2.Add(Cos(x.Mul(ExC(-2f)).Add(ExC(3f))))));
    }

    [Test]
    public void TestPow() {
        var x = VF("x");
        //x^c is special cased
        AreEqual("0", DerivDebug(x, Pow(x, ExC(0f))));
        AreEqual("1", DerivDebug(x, Pow(x, ExC(1f))));
        AreEqual("(2*x)", DerivDebug(x, Pow(x, ExC(2f))));
        AreEqual("(3*(x:>Double*x:>Double):>Single)", DerivDebug(x, Pow(x, ExC(3f))));
        AreEqual("(3.5*Math.Pow(Null,x:>Double,2.5):>Single)", DerivDebug(x, Pow(x, ExC(3.5f))));
        AreEqual("(-2*Math.Pow(Null,x:>Double,-3):>Single)", DerivDebug(x, Pow(x, ExC(-2f))));
        AreEqual("(Math.Pow(Null,x:>Double,x:>Double):>Single*(Math.Log(Null,x:>Double):>Single+(x/x)))", 
            DerivDebug(x, Pow(x, x)));
    }

    [Test]
    public void TestAssign() {
        var x = VF("x");
        var y = VF("y");
        AreEqual("((y=((2*x)+2));\n(y+(x*2));)", DerivDebug(x, Ex.Block(new[] { y }, Ex.Assign(y, E2.Mul(x).Add(E2)), x.Mul(y))));
    }

    [Test]
    public void TestPartial() {
        var bpi = new TExPI("bpi");
        AreEqual("(0.5+(0.1*bpi.index:>Single))", 
            DerivDebug(bpi.t, bpi.t.Mul(ExC(0.5f).Add(ExC(0.1f).Mul(bpi.findex)))));
        var x = VF("x");
        var y = VF("y");
        AreEqual("(y*2)", DerivDebug(x, y.Mul(2).Mul(x)));
    }
    
}
}