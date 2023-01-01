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
    private static (IAST, LexicalScope) MakeAST(ref string source, params IDelegateArg[] args) {
        var tokens = Lexer.Lex(ref source);
        var res = Reflection2.Parser.Parse(source, tokens, out var stream);
        if (res.IsRight)
            Assert.Fail(res.Right.Show(stream));
        var gs = new LexicalScope(DMKScope.Singleton);
        gs.DeclareArgs(args);
        return (res.Left.AnnotateTopLevel(gs), gs);
    }
    private static void AssertASTFail(string source, string pattern) {
        var (ast, gs) = MakeAST(ref source);
        var excs = ast.FirstPassExceptions.ToList();
        Assert.IsTrue(excs.Count > 0);
        Assert.IsTrue(excs.Any(exc => _RegexMatches(pattern, exc.Message)));
    }
    private static (IAST, LexicalScope) AssertASTOK(string source) {
        var (ast, gs) = MakeAST(ref source);
        Assert.IsTrue(!ast.FirstPassExceptions.Any());
        return (ast, gs);
    }
    private static void AssertTypecheckFail(string source, string pattern) {
        var (ast, gs) = AssertASTOK(source);
        var resolver = new TypeResolver(DMKScope.TypeConversions);
        var typ = IAST.Typecheck(ast, resolver, gs);
        if (typ.IsRight)
            RegexMatches(pattern, IAST.EnrichError(typ.Right).Message);
        else
            Assert.Fail("Expected typecheck to fail");
    }
    private static void AssertTypecheckOK(string source) {
        var (ast, gs) = AssertASTOK(source);
        var resolver = new TypeResolver(DMKScope.TypeConversions);
        var typ = IAST.Typecheck(ast, resolver, gs);
        Debug.Log(ast.DebugPrintStringify());
        if (typ.IsRight)
            Assert.Fail(IAST.EnrichError(typ.Right).Message);
    }
    [Test]
    public static void GroupingFailure() {
        AssertTypecheckFail("s tprot px 4 + pxy 4 3", "Typechecking failed for method T Add");
        AssertTypecheckOK("s tprot(px 4 + pxy 4 3)");
        AssertTypecheckOK("s tprot(px 4 + zero)");
        AssertTypecheckOK(@"s tprot(px block {
    vtp_dt
} + rotate 4 rotate 3 rotate 2 rotate 1 pxy 1 2)");
    }
    
    [Test]
    public static void tmp() {
        //todo: partial functions, zero-arg functions
        //todo: environment frames (make myArg accessible from tprot)
        var source = @"var float x = 7.5
s tprot(px block {
    vtp_dt
} + rotate 4 rotate (v2x zero) rotate 2 rotate 1 pxy 1 2)
x + block {
    var y = -x + 2.9
    y++ ^ 2
}
";
        var tokens = Lexer.Lex(ref source);
        var res = Reflection2.Parser.Parse(source, tokens, out var stream);
        if (res.IsRight)
            Assert.Fail(res.Right.Show(stream));
        Debug.Log((res.Left as IDebugPrint).DebugPrintStringify());
        var gs = new LexicalScope(DMKScope.Singleton);
        var args = new IDelegateArg[] { new DelegateArg<float>("myArg") };
        gs.DeclareArgs(args);
        IAST ast = res.Left.AnnotateTopLevel(gs);
        Debug.Log(ast.DebugPrintStringify());
        foreach (var exc in ast.FirstPassExceptions)
            throw exc;
        var resolver = new TypeResolver(DMKScope.TypeConversions);
        var typ = IAST.Typecheck(ast, resolver, gs);
        if (typ.IsRight)
            throw IAST.EnrichError(typ.Right);
        foreach (var exc in ast.Verify()) 
            throw exc;
        Debug.Log(ast.DebugPrintStringify());
        
        var result = ast.Realize() as Func<TExArgCtx, TEx>;
        var f = CompilerHelpers.PrepareDelegate<Func<float, float>>(result, args);
        var sp = f.Compile()(1000);
        Debug.Log(sp);
        int w = 5;
    }
}
}