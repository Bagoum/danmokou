using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
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
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx>;
using ExLVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx>;
using ExSBCF = System.Func<Danmokou.Expressions.TExSBC, Danmokou.Expressions.TEx<int>, Danmokou.Expressions.TEx<BagoumLib.Cancellation.ICancellee>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx>;

namespace Danmokou.Reflection {

public interface IDelegateArg {
    public TExArgCtx.Arg MakeTExArg(int index);
}
public readonly struct DelegateArg<T> : IDelegateArg {
    private readonly string? name;
    private readonly bool isRef;
    private readonly bool priority;
    public DelegateArg(string? name, bool isRef=false, bool priority=false) {
        this.name = name;
        this.isRef = isRef;
        this.priority = priority;
    }
    public TExArgCtx.Arg MakeTExArg(int index) => TExArgCtx.Arg.Make<T>(name ?? $"arg{index+1}", priority, isRef);
    public static IDelegateArg New => new DelegateArg<T>(null);
}

public static class CompilerHelpers {
    
    #region RawCompilers

    public static D CompileDelegateLambda<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        var tac = new TExArgCtx(args);
        return exConstructor(tac).BakeAndCompile<D>(tac,
                args.Select(a => ((Expression) a.expr) is ParameterExpression p ? p : null).NotNull().ToArray());
    }
    public static D CompileDelegateLambdaBPI<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        return CompileDelegateLambda<D>(exConstructor, args.Prepend(TExArgCtx.Arg.MakeBPI).ToArray());
    }

    public static D CompileDelegateLambdaRSB<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        var arg_sb = TExArgCtx.Arg.Make<BulletManager.SimpleBullet>("sb", true, isRef: true);
        var arg_bpi = TExArgCtx.Arg.Make("sb_bpi", ((TExSB)arg_sb.expr).bpi, true);
        return CompileDelegateLambda<D>(exConstructor, args.Prepend(arg_bpi).Prepend(arg_sb).ToArray());
    }

    public static D CompileDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate =>
        CompileDelegateLambda<D>(func, args.Select((a, i) => a.MakeTExArg(i)).ToArray());
    public static D CompileDelegate<D, DR>(string func, params IDelegateArg[] args) where D : Delegate =>
        CompileDelegate<D>(func.Into<Func<TExArgCtx, TEx<DR>>>(), args);
    
    public static GCXU<T2> GCXU11<T1, T2>(Func<Func<TExArgCtx, TEx<T1>>, T2> compiler, Func<TExArgCtx, TEx<T1>> f) =>
        Automatic(compiler, f, aliases => bpi => ReflectEx.Let2(aliases, () => f(bpi), bpi), 
            (ex, mod) => tac => ex(mod(tac)));
    

    public static GCXU<D> CompileGCXU<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate =>
        Automatic(ex => CompileDelegate<D>(ex, args), func,
            alias => tac => ReflectEx.Let2(alias, () => func(tac), tac),
            (ex, mod) => tac => ex(mod(tac)));
    
    public static GCXU<D> CompileGCXU<D, DR>(string func, params IDelegateArg[] args) where D : Delegate =>
        CompileGCXU<D>(func.Into<Func<TExArgCtx, TEx<DR>>>(), args);
    
    #endregion
    
    
    private class GCXCompileResolver : ReflectEx.ICompileReferenceResolver {
        public readonly List<(Reflector.ExType, string)> bound = new List<(Reflector.ExType, string)>();

        public bool TryResolve<T>(string alias, out Ex ex) {
            var ext = Reflector.AsExType<T>();
            if (!bound.Contains((ext, alias))) {
                bound.Add((ext, alias));
            }
            ex = Ex.Default(typeof(T));
            return true;
        }
    }

    public static GCXU<T> Automatic<S, T>(Func<S, T> compiler, S exp, Func<ReflectEx.Alias[], S> relet, Func<S, Func<TExArgCtx, TExArgCtx>, S> setIcrr) {
        var resolver = new GCXCompileResolver();
        var p = compiler(setIcrr(exp, tac => {
            tac.Ctx.ICRR = resolver;
            return tac;
        }));
        if (resolver.bound.Count > 0) {
            //Automatic resolver found something, recompile
            return Expose(resolver.bound.ToArray(), compiler, exp, relet);
        } else {
            var bound = new (Reflector.ExType, string)[0];
            return (gcx, fctx) => {
                fctx.UploadAdd(bound, gcx);
                return p;
            };
        }
    }

    public static GCXU<T> Expose<S, T>((Reflector.ExType, string)[] exportVars, Func<S, T> compiler, S exp,
        Func<ReflectEx.Alias[], S> relet) {
        var aliases = new ReflectEx.Alias[exportVars.Length];
        for (int ii = 0; ii < exportVars.Length; ++ii) {
            var (ext, boundVar) = exportVars[ii];
            //The "better" way to do this would be to copy the GCX values into the let statements
            //and recompile the expression for every caller.
            //However, this is ridiculously expensive, so instead we HOIST.
            aliases[ii] = new ReflectEx.Alias(boundVar, tac => FiringCtx.GetValue(tac, ext.AsFCtxType(), boundVar));
        }
        var p = compiler((aliases.Length > 0) ? relet(aliases) : exp);
        return (gcx, fctx) => {
            fctx.UploadAdd(exportVars, gcx);
            return p;
        };
    }
}
[Reflect]
public static class Compilers {

    #region FallthroughCompilers
    
    [Fallthrough]
    [ExprCompiler]
    public static TP TP(ExTP ex) => CompileDelegateLambdaBPI<TP>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static SBV2 SBV2(ExTP ex) => CompileDelegateLambdaRSB<SBV2>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static TP3 TP3(ExTP3 ex) => CompileDelegateLambdaBPI<TP3>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static TP4 TP4(ExTP4 ex) => CompileDelegateLambdaBPI<TP4>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static VTP VTP(ExVTP ex) {
        if (ex == VTPRepo.ExNoVTP) return VTPRepo.NoVTP;
        return CompileDelegate<VTP>(tac => ex(
                tac.GetByExprType<TExMov>(),
                tac.GetByExprType<TEx<float>>(),
                tac,
                tac.GetByExprType<TExV2>()),
            new DelegateArg<Movement>("vtp_mov", true, true),
            new DelegateArg<float>("vtp_dt", true, true),
            new DelegateArg<ParametricInfo>("vtp_bpi", true, true),
            new DelegateArg<Vector2>("vtp_delta", true, true)
        );
    }

    [Fallthrough]
    [ExprCompiler]
    public static LVTP LVTP(ExVTP ex) =>
        CompileDelegate<LVTP>(tac => ex(
                tac.GetByExprType<TExLMov>(),
                tac.GetByName<float>("lvtp_dt"),
                tac,
                tac.GetByExprType<TExV2>()),
            new DelegateArg<LaserMovement>("lvtp_mov", true, true),
            new DelegateArg<float>("lvtp_dt", true),
            new DelegateArg<float>(LASER_TIME_ALIAS, true),
            new DelegateArg<ParametricInfo>("lvtp_bpi", true, true),
            new DelegateArg<Vector2>("lvtp_delta", true, true)
        );

    [Fallthrough]
    [ExprCompiler]
    public static FXY FXY(ExBPY ex) => CompileDelegateLambda<FXY>(ex, 
        TExArgCtx.Arg.Make<float>("x", true));
    
    [Fallthrough]
    [ExprCompiler]
    public static Easer Easer(ExBPY ex) => CompileDelegateLambda<Easer>(ex, 
        TExArgCtx.Arg.Make<float>("x", true));

    [Fallthrough]
    [ExprCompiler]
    public static BPY BPY(ExBPY ex) => CompileDelegateLambdaBPI<BPY>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static SBF SBF(ExBPY ex) => CompileDelegateLambda<SBF>(ex);
    

    [Fallthrough]
    [ExprCompiler]
    public static BPRV2 BPRV2(ExBPRV2 ex) => CompileDelegateLambdaBPI<BPRV2>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static Pred Pred(ExPred ex) => CompileDelegateLambdaBPI<Pred>(ex);

    [Fallthrough]
    [ExprCompiler]
    public static LPred LPred(ExPred ex) => CompileDelegate<LPred>(ex,
        new DelegateArg<ParametricInfo>("lpred_bpi", priority: true),
        new DelegateArg<float>(LASER_TIME_ALIAS)
    );

    [Fallthrough]
    [ExprCompiler]
    public static SBCF SBCF(ExSBCF ex) =>
        CompileDelegate<SBCF>(tac => {
                var sbc = tac.GetByExprType<TExSBC>();
                var ind = tac.GetByExprType<TEx<int>>();
                var ct = tac.GetByExprType<TEx<ICancellee>>();
                return ex(sbc, ind, ct, tac.AppendSB("sbcf_sbc_ref_sb", sbc[ind]));
            },
    new DelegateArg<BulletManager.AbsSimpleBulletCollection>("sbcf_sbc"),
            new DelegateArg<int>("sbcf_ii"),
            new DelegateArg<ParametricInfo>("sbcf_bpi"),
            new DelegateArg<ICancellee>("sbcf_ct")
        );

    [Fallthrough]
    [ExprCompiler]
    public static GCXF<T> GCXF<T>(Func<TExArgCtx, TEx<T>> ex) {
        //We don't make a BPI variable, instead it is assigned in the _Fake method.
        // This is because gcx.BPI is a property that creates a random ID every time it is called, which we don't want.
        return CompileDelegate<GCXF<T>>(tac => GCXFRepo._Fake(ex)(tac), new DelegateArg<GenCtx>("gcx"));
    }

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<BPY> GCXU(Func<TExArgCtx, TEx<float>> f) => GCXU11(BPY, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<Pred> GCXU(ExPred f) => GCXU11(Pred, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<TP> GCXU(ExTP f) => GCXU11(TP, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<TP3> GCXU(ExTP3 f) => GCXU11(TP3, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<TP4> GCXU(ExTP4 f) => GCXU11(TP4, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<BPRV2> GCXU(ExBPRV2 f) => GCXU11(BPRV2, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<SBF> GCXUSB(ExBPY f) => GCXU11(SBF, f);
    [Fallthrough]
    [ExprCompiler]
    public static GCXU<SBV2> GCXUSB(ExTP f) => GCXU11(SBV2, f);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<VTP> GCXU(ExVTP f) => Automatic(VTP, f, aliases => VTPRepo.LetDecl(aliases, f), 
        (ex, mod) => (a, b, tac, d) => ex(a, b, mod(tac), d));

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<LVTP> LGCXU(ExVTP f) => Automatic(LVTP, f, aliases => VTPRepo.LetDecl(aliases, f),
        (ex, mod) => (a, b, tac, d) => ex(a, b, mod(tac), d));

    #endregion

    #region GenericCompilers

    [Fallthrough]
    [ExprCompiler]
    public static Func<T1, T2, R> Compile<T1, T2, R>(Func<TExArgCtx, TEx<R>> ex) =>
        CompileDelegate<Func<T1, T2, R>>(ex, DelegateArg<T1>.New, DelegateArg<T2>.New);

    [Fallthrough]
    [ExprCompiler]
    public static GCXU<Func<T1, T2, R>> CompileGCXU<T1, T2, R>(Func<TExArgCtx, TEx<R>> ex) =>
        CompileGCXU<Func<T1, T2, R>>(ex, DelegateArg<T1>.New, DelegateArg<T2>.New);
    
    #endregion


}
}
