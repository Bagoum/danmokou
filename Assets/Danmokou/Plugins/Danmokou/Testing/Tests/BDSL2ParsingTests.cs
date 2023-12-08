using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku.Patterns;
using Danmokou.Services;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using Mizuhashi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;
using static Danmokou.Reflection2.Lexer;
using Ex = System.Linq.Expressions.Expression;
using IAST = Danmokou.Reflection2.IAST;

namespace Danmokou.Testing {

public static class BDSL2ParsingTests {
    private static (IAST, LexicalScope) MakeAST(ref string source, IDelegateArg[] args) {
        var tokens = Lexer.Lex(ref source);
        var res = Reflection2.Parser.Parse(source, tokens, out var stream);
        if (res.IsRight)
            Assert.Fail(stream.ShowAllFailures(res.Right));
        var gs = new LexicalScope(DMKScope.Singleton);
        return (res.Left.AnnotateTopLevel(gs, args), gs);
    }
    private static void AssertASTFail(string source, string pattern, IDelegateArg[] args) {
        var (ast, gs) = MakeAST(ref source, args);
        var excs = ast.FirstPassExceptions.ToList();
        Assert.IsTrue(excs.Count > 0);
        Assert.IsTrue(excs.Any(exc => _RegexMatches(pattern, exc.Message)));
    }
    private static (IAST, LexicalScope) AssertASTOK(string source, IDelegateArg[] args) {
        var (ast, gs) = MakeAST(ref source, args);
        foreach (var exc in ast.FirstPassExceptions)
            Assert.Fail(exc.Message);
        Assert.IsTrue(!ast.FirstPassExceptions.Any());
        return (ast, gs);
    }
    private static void AssertTypecheckFail(string source, string pattern, params IDelegateArg[] args) {
        var (ast, gs) = AssertASTOK(source, args);
        var typ = IAST.Typecheck(ast, gs.Root.Resolver, gs);
        if (typ.IsRight)
            RegexMatches(pattern, IAST.EnrichError(typ.Right).Message);
        else
            Assert.Fail("Expected typecheck to fail");
    }
    private static IAST AssertTypecheckOK(string source, params IDelegateArg[] args) {
        var (ast, gs) = AssertASTOK(source, args);
        var typ = IAST.Typecheck(ast, gs.Root.Resolver, gs);
        Debug.Log(ast.DebugPrintStringify());
        if (typ.IsRight)
            Assert.Fail(IAST.EnrichError(typ.Right).Message);
        return ast;
    }

    private static IAST AssertVerified(string source, params IDelegateArg[] args) {
        var ast = AssertTypecheckOK(source, args);
        foreach (var exc in ast.Verify())
            Assert.Fail(exc.Message);
        Debug.Log("\nVerified: " + ast.DebugPrintStringify());
        return ast;
    }
    
    [Test]
    public static void GroupingFailure() {
        AssertTypecheckOK(@"s tprot(px block {
    vtp_dt
} + rotate 4 rotate 3 rotate 2 rotate 1 pxy 1 2)");
        AssertTypecheckFail("s tprot px 4 + pxy 4 3", "Typechecking failed for method T Add");
        AssertTypecheckOK("s tprot(px 4 + pxy 4 3)");
        AssertTypecheckOK("s tprot(px 4 + zero)");
    }

    [Test]
    public static void MulMulRev() {
        AssertTypecheckFail("var1 * var2 * var3 * var4", "The type of variable var4 could not be determined");
    }

    [Test]
    public static void AssignCount() {
        var source = @"var float x = 7.5
var float y = 5
x++ + block {
    x += y;
    y + block {
        var z = x;
        var w = y;
        z + (myArg = w)
    }
}";
        var args = new IDelegateArg[] { new DelegateArg<float>("myArg") };
        var ast = AssertVerified(source, args);
        var vars = (ast as Reflection2.AST)!.LocalScope!.varsAndFns.Values.OrderBy(x => x.Name).ToArray();
        ListEq(vars.Select(v => (v.Assignments, v.Name)).ToArray(), new[] {
            (2, "myArg"),
            (3, "x"),
            (1, "y")
        });
        var result = ast.Realize() as Func<TExArgCtx, TEx>;
        var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(result!, args);
    }

    [Test]
    public static void NotWriteable() {
        var source = @"var float x = 5
4 + x = 3";
        var ast = AssertVerified(source);
        var result = ast.Realize() as Func<TExArgCtx, TEx>;
        try {
            var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(result!, Array.Empty<IDelegateArg>());
            Assert.Fail();
        } catch (ReflectionException exc) {
            StringContains("is not writeable", exc.Message);
        }
    }

    //[Test]
    public static void wip_controls() {
        var source = @"
var d = define-control persist restyle 'circle-red/w' (xyz = 1 && x > 2);
sync 'circle-blue/w' <> gsr2c 10 {
    scoped(b{ //ab
        var itr = 1
    })
    with-control d
} {
    erase(b{ var xyz = pm1(itr) })
    s tprot px(1.8)
    erase(b{ itr += 1 })
};
async 'circle-green/w' <1;:> gcr {
    wait 60
    times 10
    circle
    scoped(b{
        var xyz = 1
    })
    with-control d
} gsr {
    times 3
    rpp <3>
} s tprot px(2)
";
        var args = new IDelegateArg[] { };
        var ast = AssertVerified(source, args);

        var exResult = ast.Realize() as Func<TExArgCtx, TEx>;
        var result = CompilerHelpers.PrepareDelegate<Func<SyncPattern>>(exResult!, args);
        //var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(result!, args);
        //var sp = f.Compile()(1000);
        Debug.Log(5);
    }
    
    
    [Test]
    public static void tmp() {
        /* todo: scoping of gcx binders with alternate
         * eg.
         * gsr { ...
         *  binditr(myItr)
         *  alternate myItr
         * } { sp1, sp2 }
         * maybe we write this as:
         * gsr {
         *  scoped { itr = ITERATION }
         *  alternate myItr
         * } { sp1, sp2 }
         * the idea is that instead of creating a scope within all arrays (which doesn't work with alternate),
         *  we generally create a scope for each iteration of a GXR repeater (can be done by tagging all GXR methods
         *  with [CreatesInternalScope]), and put any commands that must be scoped over all children 
            in the scoped command, which runs under this internal per-iteration scope
         * BindItr can be implicitly translated to a scoped command (note: this is nontrivial, 
         * Note that this requires manual EF creation in the GXR methods, though the LexicalScope will be handled
         *  in the AST. This said it's not exactly clear how to store the LexicalScope in the GXR method,
         *  since except for GTR they're just functions. Maybe we modify the GenCtxProperties<T> param?
         * Note that it'd still be beneficial to allow statements as SyncPattern children, which we can still do with
         *  an EraseToSP:: GCXF<T>->SP or something
         */
        //rotate myArg rotate (v2x zero) rotate t rotate x pxy 1 2
        var source = @"gsr {
    circle
    times 10
    scoped(b{ //scoped has type ErasedGCXF -> GenCtxProperty
        var itr = 1;
        var xyz = 2.5
    })
} {
    s tprot px itr
    s tprot px xyz
    erase(b{ xyz += 2 })
}";
        var args = new IDelegateArg[] { };
        var ast = AssertVerified(source, args);

        var exResult = ast.Realize() as Func<TExArgCtx, TEx>;
        var result = CompilerHelpers.PrepareDelegate<Func<SyncPattern>>(exResult!, args);
        //var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(result!, args);
        //var sp = f.Compile()(1000);
        Debug.Log(5);
    }
    
    
    [Test]
    public static void tmp2() {
        var str = "gsr2c 10 { bindItr(i1) } s tprot rotate(&i1, :: { a 4 } px(&a + &a))";
        var p = IParseQueue.Lex(str);
        var ast = p.IntoAST(typeof(SyncPattern));
        int k = 5;
    }
}
}