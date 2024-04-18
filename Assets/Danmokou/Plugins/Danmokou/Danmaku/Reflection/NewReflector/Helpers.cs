using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Expressions;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Mizuhashi;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Unification.TypeDesignation;

namespace Danmokou.Reflection2 {

public delegate T ScriptFn<T>(out EnvFrame rootEf);

public delegate void ErasedScriptFn(out EnvFrame rootEf);
public static class Helpers {
    public static IDelegateArg OutEnvFrameArg => new DelegateArg<EnvFrame>("$scriptEf", true);
    
    /// <summary>
    /// (Stage 0) Convert the provided script into an ST.
    /// </summary>
    public static Either<ST.Block, ReflectionException> Parse(ref string source, out LexerMetadata metadata) {
        var tokens = Lexer.Lex(ref source, out metadata);
        var parse = Parser.Parse(source, tokens, out var stream);
        if (parse.IsRight) 
            return new ReflectionException(stream.TokenWitness.ToPosition(parse.Right.Index, parse.Right.End), 
                stream.ShowAllFailures(parse.Right));
        return parse.Left;
    }
    
    /// <summary>
    /// (Stage 1) Convert the provided script into an ST, then annotate it into an AST.
    /// <br/>This does not perform typechecking (<see cref="Typecheck"/>).
    /// <br/>The AST may have errors (<see cref="IAST.FirstPassExceptions"/>). However, these can be thrown *after*
    ///  initial typechecking.
    /// </summary>
    /// <param name="source">Script code</param>
    /// <param name="args">Top-level script arguments</param>
    /// <returns></returns>
    /// <exception cref="ReflectionException">Thrown when the script could not be parsed or there are basic errors in the AST.</exception>
    public static (IAST, LexicalScope) ParseAnnotate(ref string source, params IDelegateArg[] args) {
        var parse = Parse(ref source, out var metadata);
        if (parse.IsRight)
            throw parse.Right;
        var scope = LexicalScope.NewTopLevelScope();
        var ast = parse.Left.AnnotateWithParameters(new(scope), args).LeftOrRight<AST.Block, AST.Failure, IAST>();
        scope.SetDocComments(metadata);
        return (ast, scope);
    }

    /// <summary>
    /// (Stage 2) Typecheck the provided AST. 
    /// </summary>
    /// <param name="ast">The AST to typecheck (as returned by <see cref="ParseAnnotate"/>).</param>
    /// <param name="rootScope">The top-level scope of the AST (as returned by <see cref="ParseAnnotate"/>).</param>
    /// <param name="finalType">The required output type of the AST (eg. Vector3 or TP or StateMachine).</param>
    /// <param name="resultType">The actual output type of the AST (eg. Vector3 or TP or StateMachine).</param>
    /// <returns>The typechecked AST.</returns>
    /// <exception cref="Exception">Thrown when there are typechecking errors or if there are <see cref="IAST.FirstPassExceptions"/> errors.</exception>
    public static IAST Typecheck(this IAST ast, LexicalScope rootScope, Type? finalType, out Type resultType) {
        var typ = IAST.Typecheck(ast, rootScope.GlobalRoot.Resolver, rootScope, finalType);
        foreach (var exc in ast.FirstPassExceptions()) 
            throw exc;
        if (typ.IsRight)
            throw IAST.EnrichError(typ.Right);
        resultType = typ.Left;
        foreach (var d in ast.WarnUsage())
            d.Log();
        return ast;
    }
    
    /// <inheritdoc cref="IAST.Verify"/>
    public static IAST Finalize(this IAST ast) {
        foreach (var exc in ast.Verify())
            throw exc;
        return ast;
    }
    
    /// <summary>
    /// (Stage 4) Realize the AST into an expression function, then compile it into a delegate.
    /// </summary>
    public static D Compile<D>(this IAST ast, params IDelegateArg[] args) where D : Delegate {
        return CompilerHelpers.PrepareDelegate<D>(ast.Realize, args).Compile();
    }
    
    /// <summary>
    /// Runs all four stages of script parsing (<see cref="ParseAnnotate"/>, <see cref="Typecheck"/>,
    /// <see cref="Finalize"/>, <see cref="Compile{D}"/>) on a script.
    /// </summary>
    public static D ParseAndCompileDelegate<D>(string source, params IDelegateArg[] args) where D : Delegate {
        var (ast, gs) = ParseAnnotate(ref source, args);
        var typechecked = ast.Typecheck(gs, typeof(D).GetMethod("Invoke")!.ReturnType, out _);
        var verified = typechecked.Finalize();
        return verified.Compile<D>(args);
    }

    public static (T, EnvFrame) ParseAndCompileValue<T>(string source) {
        var fn = ParseAndCompileDelegate<ScriptFn<T>>(source, OutEnvFrameArg);
        var ret = fn(out var ef);
        return (ret, ef);
    }

    public static EnvFrame ParseAndCompileErased(string source) {
        var fn = ParseAndCompileDelegate<ErasedScriptFn>(source,OutEnvFrameArg);
        fn(out var ef);
        return ef;
    }
    
    public static string DocCommentDisplay(this IDeclaration x) => x.DocComment is { } c ? $"  \n*{c}*" : "";

    /// <summary>
    /// If this type is of the form TEx&lt;R&gt; or TExArgCtx->TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TEx or TExArgCtx->TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExOrTExFuncType(this Type t, out Type inner) {
        if (t.IsTExType(out inner))
            return true;
        if (t.IsTExFuncType(out inner))
            return true;
        inner = t;
        return false;
    }

    /// <summary>
    /// If this type is of the form TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExType(this Type t, out Type inner) {
        if (t == typeof(TEx)) {
            inner = typeof(void);
            return true;
        } else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TEx<>)) {
            inner = t.GetGenericArguments()[0];
            return true;
        }
        inner = t;
        return false;
    }
    
    /// <summary>
    /// If this type is of the form TExArgCtx->TEx&lt;R&gt;, then return true and set inner to R.
    /// <br/>If this type is TExArgCtx->TEx, then return true and set inner to void.
    /// </summary>
    public static bool IsTExFuncType(this Type t, out Type inner) {
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<,>)) {
            var gargs = t.GetGenericArguments();
            if (gargs[0] == typeof(TExArgCtx) && gargs[1].IsTExType(out inner)) {
                return true;
            }
        }
        inner = t;
        return false;
    }

    private static readonly Dictionary<Type, (Type, ConstructorInfo)> texTypeCache = new();
    /// <summary>
    /// For a type T, get the type TEx&lt;T&gt; and its constructor.
    /// </summary>
    public static (Type type, ConstructorInfo exConstructor) GetTExType(this Type simpleType) {
        if (texTypeCache.TryGetValue(simpleType, out var texTyp))
            return texTyp;
        var tt = typeof(TEx<>).MakeGenericType(simpleType);
        return texTypeCache[simpleType] = (tt, tt.GetConstructor(new[] { typeof(Expression) }));
    }

    /// <summary>
    /// Cast an expression to the type TEx&lt;T&gt;. (If t is void, then cast the expression to type TEx.)
    /// </summary>
    public static TEx MakeTypedTEx(this Type t, Ex ex) {
        if (t == typeof(void)) return ex;
        var (v, cons) = t.GetTExType();
        return (cons.Invoke(new object[] { ex }) as TEx)!;
    }

    /// <summary>
    /// Retype the provided function as TExArgCtx -> TEx{T}. (If t is void, don't retype it.)
    /// </summary>
    public static Func<TExArgCtx, TEx> MakeTypedLambda(this Type t, Func<TExArgCtx, TEx> f) =>
        TExLambdaTyper.ConvertForType(t, f);

    public class NotWriteableException : Exception {
        public int ArgIndex { get; }

        public NotWriteableException(int argIndex, string err) : base(err) {
            this.ArgIndex = argIndex;
        }

        public NotWriteableException(int argIndex, string err, Exception inner) : base(err) {
            this.ArgIndex = argIndex;
        }
    }

    /// <summary>
    /// Return an exception if the provided expression is not writeable.
    /// <br/>Implementation based on https://source.dot.net/#System.Linq.Expressions/System/Linq/Expressions/Expression.cs,241
    /// </summary>
    public static NotWriteableException? AssertWriteable(int argIndex, object prm) {
        NotWriteableException Err(string err) => new(argIndex, err);
        Expression ex;
        try {
            if (prm is TEx tex)
                ex = tex;
            else
                ex = (Ex)prm;
        } catch (Exception ecx) {
            return new(argIndex, $"Failure: this is not an expression. Please report this.", ecx);
        }
        switch (ex) {
            case IndexExpression iex:
                if (iex.Indexer?.CanWrite is false)
                    return Err("This is an indexing expression, but the indexer is not writeable.");
                return null;
            case MemberExpression mex:
                return mex.Member switch {
                    PropertyInfo p => p.CanWrite ?
                        null :
                        Err($"The property {p.Name} is not writeable."),
                    FieldInfo f => !(f.IsInitOnly || f.IsLiteral) ?
                        null :
                        Err($"The field {f.Name} is not writeable."),
                    { } m => Err($"Member {m.Name} is of an unhandled type {m.GetType().SimpRName()}")
                };
            case ParameterExpression:
                return null;
            default:
                return Err($"This expression has type {ex.GetType().SimpRName()}:{ex.NodeType}, which is not writeable." +
                           " A writeable expression is an array indexer, property, field, or variable.");
            
        }
        
    }
    
}

public static class TExLambdaTyper {
    private static readonly Dictionary<Type, MethodInfo> converters = new();
    private static readonly MethodInfo cmi = typeof(TExLambdaTyper).GetMethod(nameof(Convert))!;

    public static Func<TExArgCtx, TEx<T>> Convert<T>(Func<TExArgCtx, TEx> f) => tac => (Ex)f(tac);
    public static Func<TExArgCtx, TEx> ConvertForType(Type t, Func<TExArgCtx, TEx> f) {
        if (t == typeof(void)) return f;
        var conv = converters.TryGetValue(t, out var c) ? c : converters[t] = cmi.MakeGenericMethod(t);
        return (conv.Invoke(null, new object[] { f }) as Func<TExArgCtx, TEx>)!;
    }
}

}