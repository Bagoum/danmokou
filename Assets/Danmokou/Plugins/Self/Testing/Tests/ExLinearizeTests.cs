using System;
using System.Linq.Expressions;
using NUnit.Framework;
using Ex = System.Linq.Expressions.Expression;
using DMK.DMath.Functions;
using DMK.Expressions;
using FastExpressionCompiler;
using static DMK.Expressions.ExMHelpers;
using static NUnit.Framework.Assert;
using static DMK.DMath.Functions.ExM;

namespace DMK.Testing {

public class ExLinearizeTests {
    private static float Compile(Ex ex) => Expression.Lambda<Func<float>>(ex).Compile()();
    private static T Compile<T>(Ex ex) => Expression.Lambda<Func<T>>(ex).Compile()();
    private static Func<float,float> Compile1(Ex ex, ParameterExpression pex) => Expression.Lambda<Func<float, float>>(ex, pex).Compile();
    private static ParameterExpression VF(string name) => Ex.Variable(typeof(float), name);
    private static ParameterExpression V<T>(string name) => Ex.Variable(typeof(T), name);

    private static void DebugsTo(Expression ex, string expected) => 
        AreEqual(expected, ex.Debug());

    private static void LinDebugsTo(Expression ex, string expected) =>
        AreEqual(expected.Replace("\r\n", "\n"), ex.Linearize().Debug());

    [Test]
    public void TestAssign() {
        var x = VF("x");
        var yi = VF("y");
        var zi = VF("z");

        var ex = x.Is(Ex.Block(new[] {yi},
            yi.Is(ExC(5f)),
            yi.Add(x)
        ));
        
        DebugsTo(ex, "(x=((y=5);\n(y+x);))");
        LinDebugsTo(ex, "((y=5);\n(x=(y+x));)");

        AreEqual(11f, Compile1(ex.Linearize(), x)(6));

        var ex2 = Ex.Block(
            x.Is(Ex.Block(new[] {yi},
                yi.Is(ExC(5f)),
                yi.Add(x)
            ).Add(Ex.Block(new[] {zi},
                zi.Is(x),
                zi.Mul(2f)
            ))),
            x.Add(2f)
        );
        
        DebugsTo(ex2, "((x=(((y=5);\n(y+x);)+((z=x);\n(z*2);)));\n(x+2);)");
        LinDebugsTo(ex2, @"((y=5);
(z=x);
(x=((y+x)+(z*2)));
(x+2);)");
    }

    [Test]
    public void TestCond() {
        var x = VF("x");
        var yi = VF("y");
        var zi = VF("z");
        var ex = Ex.Condition(Ex.Block(zi.Is(ExC(5f)), ExC(true)),
            Ex.Block(new[] {yi},
                yi.Is(ExC(5f)),
                yi.Add(x)),
            ExC(2f)
        );
        AreEqual(ex.Linearize().ToCSharpString(), @"
    float linz_0;
    z = (5f);
    if (true) {
        float y;
        y = (5f);
        linz_0 = ((y) + (x));
    } else {
        linz_0 = (2f);
    }
    
    linz_0;;");
        //ternary ok
        var ex2 = Ex.Condition(Ex.Block(zi.Is(ExC(5f)), ExC(true)),
            yi.Add(x),
            ExC(2f)
        );
        AreEqual(ex2.Linearize().ToCSharpString(), @"
    z = (5f);
    (true ?
        (y) + (x) :
        2f);;");
        //cond can be simplified
        var ex3 = Ex.Condition(Ex.GreaterThan(zi, ExC(5f)),
            yi.Add(x),
            ExC(2f)
        );
        AreEqual(ex3.Linearize().ToCSharpString(), @"((z) > (5f) ?
    (y) + (x) :
    2f);");


    }
    
}
}