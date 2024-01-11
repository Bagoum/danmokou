using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection.CustomData;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using PEx = System.Linq.Expressions.ParameterExpression;
using static Danmokou.Reflection.Aliases;
using static Danmokou.Reflection.CompilerHelpers;

using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector4>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV3, Danmokou.Expressions.TEx>;
using ExSBCF = System.Func<Danmokou.Expressions.TExSBCUpdater, Danmokou.Expressions.TEx<BagoumLib.Cancellation.ICancellee>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx>;

namespace Danmokou.Reflection {

public interface IDelegateArg {
    public TExArgCtx.Arg MakeTExArg(int index);
    public ImplicitArgDecl MakeImplicitArgDecl();
    public string Name { get; }
    /// <summary>
    /// Type of function argument, on the level of typeof(float).
    /// </summary>
    public Type Type { get; }
}
public readonly struct DelegateArg<T> : IDelegateArg {

    public string Name { get; }
    public Type Type => typeof(T);
    private readonly bool isRef;
    private readonly bool priority;
    public DelegateArg(string name, bool isRef=false, bool priority=false) {
        this.Name = name;
        this.isRef = isRef;
        this.priority = priority;
    }
    public TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.Make<T>(Name ?? $"$_arg{index+1}", priority, isRef);
    public ImplicitArgDecl MakeImplicitArgDecl() => new ImplicitArgDecl<T>(default, Name!);
}

/// <summary>
/// A layer of indirection placed after expression construction but before compilation in order to allow efficient handling of two-pass expressions with <see cref="CompilerHelpers.Automatic{S,T}"/>.
/// </summary>
public readonly struct ReadyToCompileExpr<D> where D : Delegate {
    private readonly TEx expression;
    private readonly TExArgCtx argBag;
    private readonly TExArgCtx.Arg[] arguments;
    private readonly D? fixedResult;
    public ReadyToCompileExpr(TEx expression, TExArgCtx argBag, TExArgCtx.Arg[] arguments) {
        this.expression = expression;
        this.argBag = argBag;
        this.arguments = arguments;
        fixedResult = null;
    }

    public ReadyToCompileExpr(D fixedResult) {
        expression = null!;
        argBag = null!;
        arguments = null!;
        this.fixedResult = fixedResult;
    }

    public D Compile() => 
        fixedResult ?? expression.BakeAndCompile<D>(argBag,
        arguments.Select(a => (Expression)a.expr as ParameterExpression ?? null).FilterNone().ToArray());

    //public static implicit operator D(ReadyToCompileExpr<D> expr) => expr.Compile();
}

public static class CompilerHelpers {
    
    #region RawCompilers

    public static ReadyToCompileExpr<D> PrepareDelegate<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        //Implicitly add an EnvFrame argument if ParametricInfo or GCX is present
        Ex? bpiArg = null;
        Ex? gcxArg = null;
        var hasEFArg = false;
        for (int ii = 0; ii < args.Length; ++ii) {
            bpiArg ??= args[ii].expr.type == typeof(ParametricInfo) ? (Ex)args[ii].expr : null;
            gcxArg ??= args[ii].expr.type == typeof(GenCtx) ? (Ex)args[ii].expr : null;
            hasEFArg |= args[ii].expr.type == typeof(EnvFrame);
        }
        if (!hasEFArg) {
            if (bpiArg != null)
                args = args.Append(TExArgCtx.Arg.Make("ef", new TExPI(bpiArg).EnvFrame, false)).ToArray();
            else if (gcxArg != null)
                args = args.Append(TExArgCtx.Arg.Make("ef", new TExGCX(gcxArg).EnvFrame, false)).ToArray();
        }
        var tac = new TExArgCtx(args);
        return new(exConstructor(tac), tac, args);
    }

    public static ReadyToCompileExpr<D> PrepareDelegateBPI<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        return PrepareDelegate<D>(exConstructor, args.Prepend(TExArgCtx.Arg.MakeBPI).ToArray());
    }
    public static ReadyToCompileExpr<D> PrepareDelegateBPI<D>(Func<TExArgCtx, TEx> exConstructor) where D : Delegate {
        return PrepareDelegate<D>(exConstructor, TExArgCtx.Arg.MakeBPI);
    }

    public static ReadyToCompileExpr<D> PrepareDelegateRSB<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        var arg_sb = TExArgCtx.Arg.Make<BulletManager.SimpleBullet>("sb", true, isRef: true);
        var arg_bpi = TExArgCtx.Arg.Make("sb_bpi", ((TExSB)arg_sb.expr).bpi, true);
        return PrepareDelegate<D>(exConstructor, args.Prepend(arg_bpi).Prepend(arg_sb).ToArray());
    }

    public static ReadyToCompileExpr<D> PrepareDelegateRSB<D>(Func<TExArgCtx, TEx> exConstructor) where D : Delegate =>
        PrepareDelegateRSB<D>(exConstructor, Array.Empty<TExArgCtx.Arg>());

    public static ReadyToCompileExpr<D> PrepareDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate =>
        PrepareDelegate<D>(func, args.Select((a, i) => a.MakeTExArg(i)).ToArray());

    //Note: while there is a theoretical overhead to deriving the return type at runtime,
    // this function is not called particularly often, so it's not a bottleneck.
    public static D CompileDelegate<D>(string func, params IDelegateArg[] args) where D : Delegate {
        var returnType = typeof(D).GetMethod("Invoke")!.ReturnType;
        var exType = Reflector.Func2Type(typeof(TExArgCtx), typeof(TEx<>).MakeGenericType(returnType));
        
        return PrepareDelegate<D>((func.Into(exType) as Func<TExArgCtx, TEx>)!, args).Compile();
    }

    public static GCXU<T2> GCXU11<T1, T2>(Func<Func<TExArgCtx, TEx<T1>>, ReadyToCompileExpr<T2>> compiler, Func<TExArgCtx, TEx<T1>> f) where T2 : Delegate =>
        Automatic(compiler, f, (ex, aliases) => tac => ReflectEx.LetAlias(aliases, () => ex(tac), tac), 
            (ex, mod) => tac => ex(mod(tac)));
    
    
    public static GCXU<D> CompileGCXU<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate =>
        Automatic(ex => PrepareDelegate<D>(ex, args), func,
            (ex, aliases) => tac => ReflectEx.LetAlias(aliases, () => ex(tac), tac),
            (ex, mod) => tac => ex(mod(tac)));
    
    public static GCXU<D> CompileGCXU<D, DR>(string func, params IDelegateArg[] args) where D : Delegate =>
        CompileGCXU<D>(func.Into<Func<TExArgCtx, TEx<DR>>>(), args);
    
    #endregion
    
    
    /// <summary>
    /// Tracks references to variables from GCX which have not been exposed.
    /// <br/>This is used in the first phase of GCXU compilation, and marked variables
    ///  will be automatically exposed during the second phase.
    /// </summary>
    private class GCXUCompileResolver : ReflectEx.ICompileReferenceResolver {
        public int TotalUsages { get; private set; } = 0;
        private readonly List<(Type, string)> bound = new();
        public IReadOnlyList<(Type, string)> Bound => bound;
        public bool RequiresBinding => bound.Count > 0;
        private readonly Dictionary<(Type, string), int> counters = new();
        private readonly HashSet<(Type, string)> dirty = new();

        
        public bool TryResolve(TExArgCtx tac, Type t, string alias, out Ex ex) {
            if (!counters.ContainsKey((t, alias))) {
                bound.Add((t, alias));
                counters[(t, alias)] = 0;
            }
            ++counters[(t, alias)];
            ++TotalUsages;
            ex = Ex.Variable(t, "$deferred_gcxu_variable");
            return true;
        }
        public void MarkDirty(Type t, string alias) => dirty.Add((t, alias));

        public static string FrameVarCSEName(int parentage, Type type) => $"$fv{type.RName()}_{parentage}";

        public ReflectEx.Alias[] ToAliases(ReflectEx.Alias? extra) {
            int exOffset = (extra.HasValue ? 1 : 0);
            var aliases = new ReflectEx.Alias[exOffset + Bound.Count];
            if (extra.Try(out var ex))
                aliases[0] = ex;
            for (int ii = 0; ii < Bound.Count; ++ii) {
                var (t, s) = Bound[ii];
                aliases[exOffset + Bound.Count + ii] = 
                    new ReflectEx.Alias(t, s, tac => PICustomData.GetValue(tac, t, s)) {
                        DirectAssignment = counters[(t, s)] == 1 || dirty.Contains((t, s))
                    };
            }
            return aliases;
        }
    }

    /// <summary>
    /// A dummy reference resolver that always returns false.
    /// <br/>This is used to mark that the GCXU compilation is in its second phase.
    /// </summary>
    public class GCXUDummyResolver : ReflectEx.ICompileReferenceResolver {
        public static readonly GCXUDummyResolver Singleton = new();

        public bool TryResolve(TExArgCtx tac, Type t, string alias, out Expression ex) {
            ex = default!;
            return false;
        }

        public void MarkDirty(Type t, string alias) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Detect if there are any bound variables in the provided expression function,
    ///  and handle exposing them if there are.
    /// </summary>
    /// <param name="compiler">Function that compiles the expression function into a delegate</param>
    /// <param name="exp">Expression function (eg. TExArgCtx -> TEx)</param>
    /// <param name="modifyLets">Function that modifies the expression function by exposing variables</param>
    /// <param name="modifyArgBag">Function that modifies the input to the expression function</param>
    /// <typeparam name="S">Expression function (eg. TExArgCtx -> TEx)</typeparam>
    /// <typeparam name="T">Delegate type (eg. BPY)</typeparam>
    /// <returns></returns>
    public static GCXU<T> Automatic<S, T>(Func<S, ReadyToCompileExpr<T>> compiler, S exp, Func<S, ReflectEx.Alias[], S> modifyLets, Func<S, Func<TExArgCtx, TExArgCtx>, S> modifyArgBag) where T : Delegate {
        var resolver = new GCXUCompileResolver();
        LexicalScope enclosingScope = null!;
        var ww = compiler(modifyArgBag(exp, tac => {
            enclosingScope = tac.Ctx.Scope;
            tac.Ctx.GCXURefs = resolver;
            return tac;
        }));
        return new GCXU<T>(resolver.Bound, enclosingScope, type => {
            ReflectEx.Alias? bpiAsTypeAlias = null;
            if (resolver.RequiresBinding) {
                //When there are multiple usages of (bpi.data as CustomDataType), cache the value of that in an local variable.
                //Note: in practice this optimization for type-as doesn't seem to do much, probably because MSIL automatically
                // optimizes for it even if you don't do it here.
                if (resolver.TotalUsages > 1 && !PICustomDataBuilder.DISABLE_TYPE_BUILDING) {
                    bpiAsTypeAlias = new ReflectEx.Alias(type.BuiltType, PICustomData.bpiAsCustomDataTypeAlias, 
                        tac => tac.BPI.FiringCtx.As(type.BuiltType));
                }
                exp = modifyLets(exp, resolver.ToAliases(bpiAsTypeAlias));
            }
            return compiler(modifyArgBag(exp, tac => {
                tac.Ctx.Scope = enclosingScope;
                tac.Ctx.GCXURefs = GCXUDummyResolver.Singleton;
                tac.Ctx.CustomDataType = (type.BuiltType,
                    bpiAsTypeAlias.Try(out var alias) ?
                        //Read the local variable for (bpi.data as CustomDataType) if we set it above
                        tac => ReflectEx.GetAliasFromStack(alias.alias, tac) ??
                               throw new Exception("Couldn't find bpi-as-customtype alias on the stack")
                        //Otherwise recalculate it
                        : tac => tac.BPI.FiringCtx.As(type.BuiltType));
                return tac;
            })).Compile();
        });
    }

}
[Reflect]
public static class Compilers {
    /// <summary>
    /// Assert that the variables provided are stored in the bullet's custom data, then execute the inner content.
    /// <br/>Since <see cref="GCXU{Fn}"/> automatically stores variables used in its scope, you generally only
    /// need to call this function when the variables will be used by some other scope, such as bullet controls.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> Expose<T>((Reflector.ExType, string)[] variables, Func<TExArgCtx, TEx<T>> inner) => tac => {
        foreach (var (ext, name) in variables)
            tac.Ctx.GCXURefs?.TryResolve(tac, ext.AsType(), name, out _);
        return inner(tac);
    };
    

    #region FallthroughCompilers
    
    [Fallthrough]
    [ExprCompiler]
    public static TP TP(ExTP ex) => PrepareDelegateBPI<TP>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static SBV2 SBV2(ExTP ex) => PrepareDelegateRSB<SBV2>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static TP3 TP3(ExTP3 ex) => PrepareDelegateBPI<TP3>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static TP4 TP4(ExTP4 ex) => PrepareDelegateBPI<TP4>(ex).Compile();

    private static ReadyToCompileExpr<VTP> _VTP(ExVTP ex) {
        if (ex == VTPRepo.ExNoVTP) return new(VTPRepo.NoVTP);
        return PrepareDelegate<VTP>(tac => ex(
                tac.GetByExprType<TExMov>(),
                tac.GetByExprType<TEx<float>>(),
                tac,
                tac.GetByExprType<TExV3>()),
            VTPArgs
        );
    }

    public static readonly IDelegateArg[] VTPArgs = {
        new DelegateArg<Movement>("vtp_mov", true, true),
        new DelegateArg<float>("vtp_dt", true, true),
        new DelegateArg<ParametricInfo>("vtp_bpi", true, true),
        new DelegateArg<Vector3>("vtp_delta", true, true)
    };

    [Fallthrough]
    [ExprCompiler]
    public static VTP VTP(ExVTP ex) => _VTP(ex).Compile();

    private static ReadyToCompileExpr<LVTP> _LVTP(ExVTP ex) =>
        PrepareDelegate<LVTP>(tac => ex(
                tac.GetByExprType<TExLMov>(),
                tac.GetByName<float>("lvtp_dt"),
                tac,
                tac.GetByExprType<TExV3>()),
            new DelegateArg<LaserMovement>("lvtp_mov", true, true),
            new DelegateArg<float>("lvtp_dt", true),
            new DelegateArg<float>(LASER_TIME_ALIAS, true),
            new DelegateArg<ParametricInfo>("lvtp_bpi", true, true),
            new DelegateArg<Vector3>("lvtp_delta", true, true)
        );
    [Fallthrough]
    [ExprCompiler]
    public static LVTP LVTP(ExVTP ex) =>
        _LVTP(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static FXY FXY(ExBPY ex) => PrepareDelegate<FXY>(ex, 
        TExArgCtx.Arg.Make<float>("x", true)).Compile();
    
    [Fallthrough]
    [ExprCompiler]
    public static Easer Easer(ExBPY ex) => PrepareDelegate<Easer>(ex, 
        TExArgCtx.Arg.Make<float>("x", true)).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static BPY BPY(ExBPY ex) => PrepareDelegateBPI<BPY>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static SBF SBF(ExBPY ex) => PrepareDelegateRSB<SBF>(ex).Compile();
    

    [Fallthrough]
    [ExprCompiler]
    public static BPRV2 BPRV2(ExBPRV2 ex) => PrepareDelegateBPI<BPRV2>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static Pred Pred(ExPred ex) => PrepareDelegateBPI<Pred>(ex).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static LPred LPred(ExPred ex) => PrepareDelegate<LPred>(ex,
        new DelegateArg<ParametricInfo>("lpred_bpi", priority: true),
        new DelegateArg<float>(LASER_TIME_ALIAS)
    ).Compile();

    [Fallthrough]
    [ExprCompiler]
    public static SBCF SBCF(ExSBCF ex) =>
        PrepareDelegate<SBCF>(tac => {
                var st = tac.GetByExprType<TExSBCUpdater>();
                var ct = tac.GetByExprType<TEx<ICancellee>>();
                return ex(st, ct, tac.AppendSB("sbcf_sbc_ref_sb", st.sb));
            },
    new DelegateArg<BulletManager.SimpleBulletCollection.VelocityUpdateState>("sbcf_updater", true),
            new DelegateArg<ParametricInfo>("sbcf_bpi", true),
            new DelegateArg<ICancellee>("sbcf_ct", true)
        ).Compile();

    public static readonly IDelegateArg[] GCXFArgs = {
        new DelegateArg<GenCtx>("gcx")
    };
    
    [Fallthrough]
    [ExprCompiler]
    public static GCXF<T> GCXF<T>(Func<TExArgCtx, TEx<T>> ex) {
        //We don't make a BPI variable, instead it is assigned in the _Fake method.
        // This is because gcx.BPI is a property that creates a random ID every time it is called, which we don't want.
        return PrepareDelegate<GCXF<T>>(tac => GCXFRepo._Fake(ex)(tac), GCXFArgs).Compile();
    }
    
    [Fallthrough]
    [ExprCompiler]
    public static ErasedGCXF ErasedGCXF<T>(Func<TExArgCtx, TEx<T>> ex) {
        //We don't make a BPI variable, instead it is assigned in the _Fake method.
        // This is because gcx.BPI is a property that creates a random ID every time it is called, which we don't want.
        return PrepareDelegate<ErasedGCXF>(tac => Ex.Block(GCXFRepo._Fake(ex)(tac), Ex.Empty()), 
            GCXFArgs).Compile();
    }

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<BPY> GCXU(ExBPY f) => GCXU11(PrepareDelegateBPI<BPY>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<Pred> GCXU(ExPred f) => GCXU11(PrepareDelegateBPI<Pred>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<TP> GCXU(ExTP f) => GCXU11(PrepareDelegateBPI<TP>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<TP3> GCXU(ExTP3 f) => GCXU11(PrepareDelegateBPI<TP3>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<TP4> GCXU(ExTP4 f) => GCXU11(PrepareDelegateBPI<TP4>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<BPRV2> GCXU(ExBPRV2 f) => GCXU11(PrepareDelegateBPI<BPRV2>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<SBF> GCXUSB(ExBPY f) => GCXU11(PrepareDelegateRSB<SBF>, f);
    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<SBV2> GCXUSB(ExTP f) => GCXU11(PrepareDelegateRSB<SBV2>, f);

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<VTP> GCXU(ExVTP f) => Automatic(_VTP, f, (ex, aliases) => VTPRepo.Let(aliases, ex), 
        (ex, mod) => (a, b, tac, d) => ex(a, b, mod(tac), d));

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<LVTP> LGCXU(ExVTP f) => Automatic(_LVTP, f, (ex, aliases) => VTPRepo.Let(aliases, ex),
        (ex, mod) => (a, b, tac, d) => ex(a, b, mod(tac), d));

    #endregion

    #region GenericCompilers

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static Func<T1, T2, R> Compile<T1, T2, R>(Func<TExArgCtx, TEx<R>> ex) =>
        PrepareDelegate<Func<T1, T2, R>>(ex, new DelegateArg<T1>("arg1"), new DelegateArg<T2>("arg2")).Compile();

    [Fallthrough]
    [ExprCompiler]
    [ExtendGCXUExposed]
    public static GCXU<Func<T1, T2, R>> CompileGCXU<T1, T2, R>(Func<TExArgCtx, TEx<R>> ex) =>
        CompileGCXU<Func<T1, T2, R>>(ex, new DelegateArg<T1>("arg1"), new DelegateArg<T2>("arg2"));
    
    #endregion


}
}
