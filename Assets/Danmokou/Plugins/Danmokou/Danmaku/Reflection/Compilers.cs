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
    
    #endregion

}
[Reflect]
public static class Compilers {
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

    #endregion
    
}
}
