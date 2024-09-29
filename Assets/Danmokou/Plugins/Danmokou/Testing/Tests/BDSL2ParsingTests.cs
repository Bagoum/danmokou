using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Functional;
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
using AST = Danmokou.Reflection2.AST;
using Ex = System.Linq.Expressions.Expression;
using Helpers = Danmokou.Reflection2.Helpers;
using IAST = Danmokou.Reflection2.IAST;

namespace Danmokou.Testing {

public static class BDSL2ParsingTests {
    private static (IAST, LexicalScope) MakeAST(ref string source, IDelegateArg[] args) {
        var tokens = Lexer.Lex(ref source, out _);
        var res = Reflection2.Parser.Parse(source, tokens, out var stream);
        if (res.IsRight)
            Assert.Fail(stream.ShowAllFailures(res.Right));
        var gs = LexicalScope.NewTopLevelScope();
        return (res.Left.AnnotateWithParameters(new(gs), args).LeftOrRight<AST.Block, AST.Failure, IAST>(), gs);
    }
    private static void AssertASTFail(string source, string pattern, IDelegateArg[] args) {
        var (ast, _) = MakeAST(ref source, args);
        var excs = ast.FirstPassExceptions().ToList();
        Assert.IsTrue(excs.Count > 0);
        Assert.IsTrue(excs.Any(exc => _RegexMatches(pattern, exc.Message)));
    }
    private static (IAST, LexicalScope) AssertASTOK(string source, IDelegateArg[] args) {
        var (ast, gs) = MakeAST(ref source, args);
        foreach (var exc in ast.FirstPassExceptions())
            Assert.Fail(exc.Message);
        Assert.IsTrue(!ast.FirstPassExceptions().Any());
        return (ast, gs);
    }
    private static void AssertTypecheckFail(string source, string pattern, params IDelegateArg[] args) {
        var (ast, gs) = AssertASTOK(source, args);
        var typ = IAST.Typecheck(ast, gs.GlobalRoot.Resolver, gs);
        if (typ.IsRight)
            RegexMatches(pattern, IAST.EnrichError(typ.Right).Message);
        else
            Assert.Fail("Expected typecheck to fail");
    }
    private static IAST AssertTypecheckOK(string source, params IDelegateArg[] args) {
        var (ast, gs) = AssertASTOK(source, args);
        var typ = IAST.Typecheck(ast, gs.GlobalRoot.Resolver, gs);
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
    t
} + rotate 4 rotate 3 rotate 2 rotate 1 pxy 1 2)");
        AssertTypecheckFail("s tprot px 4 + pxy 4 3", "Typechecking failed for method T Add");
        AssertTypecheckOK("s tprot(px 4 + pxy 4 3)");
        AssertTypecheckOK("s tprot(px 4 + zero)");
    }

    [Test]
    public static void AssignCount() {
        var source = @"var x::float = 7.5
var y::float = 5
x++ + block {
    x += y;
    y + block {
        var z = x;
        var w = y;
        z + (myArg += w)
    }
}";
        var args = new IDelegateArg[] { new DelegateArg<float>("myArg") };
        var ast = AssertVerified(source, args);
        var vars = (ast as AST)!.LocalScope!.variableDecls.Values.OrderBy(x => x.Name).ToArray();
        ListEq(vars.Select(v => (v.Assignments, v.Name)).ToArray(), new[] {
            (2, "myArg"),
            (3, "x"),
            (1, "y")
        });
        var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(ast.Realize, args).Compile();
        Assert.AreEqual(f(100), 131);
    }

    public delegate float RefFunc(ref float y);

    [Test]
    public static void Add1() {
        var source = @"x += 1";
        var args = new IDelegateArg[] { new DelegateArg<float>("x") };
        var f = Helpers.ParseAndCompileDelegate<Func<float, float>>(source, args);
        Assert.AreEqual(f(12.4f), 13.4f);
        var x = 20.1f;
        var rargs = new IDelegateArg[] { new DelegateArg<float>("x", isRef: true) };
        var rf = Helpers.ParseAndCompileDelegate<RefFunc>(source, rargs);
        Assert.AreEqual(rf(ref x), 21.1f);
        Assert.AreEqual(x, 21.1f);
    }

    [Test]
    public static void NotWriteable() {
        var source = @"var x::float = 5
4 + x = 3";
        var ast = AssertVerified(source);
        try {
            CompilerHelpers.PrepareDelegate<Func<float, float>>(ast.Realize, Array.Empty<IDelegateArg>());
            Assert.Fail();
        } catch (ReflectionException exc) {
            StringContains("is not writeable", exc.Message);
        }
    }
    
    [Test]
    public static void tmp2() {
        var args = new IDelegateArg[] { };
        var ast = AssertVerified("gsr2c 10 { bindLR() } { s tprot rotate(lr * 20, block{ var aa = 4; px(aa + aa); }) }");
        var result = CompilerHelpers.PrepareDelegate<Func<SyncPattern>>(ast.Realize, args);
        Debug.Log(result);
    }
}
}