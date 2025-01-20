using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
using Danmokou.SM;
using JetBrains.Annotations;
using Scriptor;
using Scriptor.Analysis;
using Scriptor.Expressions;
using Scriptor.Reflection;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Reflection.Aliases;
using static Danmokou.Reflection.CompilerHelpers;
using static Scriptor.Expressions.ExHelpers;

using ExBPY = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<float>>;
using ExPred = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<bool>>;
using ExTP = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<UnityEngine.Vector4>>;
using ExBPRV2 = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<BagoumLib.Mathematics.V2RV2>>;
using ExVTP = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<Danmokou.Expressions.VTPExpr>>;
using ExSBCF = System.Func<Danmokou.Expressions.TExSBCUpdater, Scriptor.Expressions.TEx<BagoumLib.Cancellation.ICancellee>, Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx>;

namespace Danmokou.Reflection {
/// <summary>
/// A layer of indirection placed after expression construction but before compilation.
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
        arguments.Select(a => (Ex)a.expr as ParameterExpression).FilterNone().ToArray());

    //public static implicit operator D(ReadyToCompileExpr<D> expr) => expr.Compile();
}

public static class CompilerHelpers {
    private static TExArgCtx.Arg MakeBPI() => MakeArg<ParametricInfo>("bpi", true);
    
    #region RawCompilers

    public static ReadyToCompileExpr<D> PrepareDelegate<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        //Implicitly add an EnvFrame argument if ParametricInfo or GCX is present
        //Implicitly add a ParametricInfo argument if GCX is present
        TExPI? bpiArg = null;
        TExGCX? gcxArg = null;
        TExSBCUpdater? sbcUpdArg = null;
        var sbArg = false;
        var hasEFArg = false;
        for (int ii = 0; ii < args.Length; ++ii) {
            bpiArg ??= args[ii].expr as TExPI;
            gcxArg ??= args[ii].expr as TExGCX;
            sbcUpdArg ??= args[ii].expr as TExSBCUpdater;
            sbArg |= args[ii].expr is TExSB;
            hasEFArg |= args[ii].expr.type == typeof(EnvFrame);
        }
        if (!sbArg && sbcUpdArg != null) {
            args = args.Append(TExArgCtx.Arg.FromTEx("sbcf_sbc_ref_sb", sbcUpdArg.sb, true)).ToArray();
        }
        if (!hasEFArg) {
            if (bpiArg != null)
                args = args.Append(TExArgCtx.Arg.FromTEx("ef", bpiArg.EnvFrame, false)).ToArray();
            else if (gcxArg != null)
                args = args.Append(TExArgCtx.Arg.FromTEx("ef", gcxArg.EnvFrame, false)).ToArray();
        }
        if (bpiArg is null) {
            if (gcxArg != null)
                args = args.Append(TExArgCtx.Arg.FromTEx("gcx_bpi", new TExPI(new TExGCX(gcxArg).bpi), true)).ToArray();
        }
        var tac = new TExArgCtx(args);
        return new(exConstructor(tac), tac, args);
    }

    public static ReadyToCompileExpr<D> PrepareDelegateBPI<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        return PrepareDelegate<D>(exConstructor, args.Prepend(MakeBPI()).ToArray());
    }
    public static ReadyToCompileExpr<D> PrepareDelegateBPI<D>(Func<TExArgCtx, TEx> exConstructor) where D : Delegate {
        return PrepareDelegate<D>(exConstructor, MakeBPI());
    }

    public static ReadyToCompileExpr<D> PrepareDelegateRSB<D>(Func<TExArgCtx, TEx> exConstructor, params TExArgCtx.Arg[] args) where D : Delegate {
        var arg_sb = MakeArg<BulletManager.SimpleBullet>("sb", true, isRef: true);
        var arg_bpi = TExArgCtx.Arg.FromTEx("sb_bpi", ((TExSB)arg_sb.expr).bpi, true);
        return PrepareDelegate<D>(exConstructor, args.Prepend(arg_bpi).Prepend(arg_sb).ToArray());
    }

    public static ReadyToCompileExpr<D> PrepareDelegateRSB<D>(Func<TExArgCtx, TEx> exConstructor) where D : Delegate =>
        PrepareDelegateRSB<D>(exConstructor, Array.Empty<TExArgCtx.Arg>());

    public static ReadyToCompileExpr<D> PrepareDelegate<D>(Func<TExArgCtx, TEx> func, params IDelegateArg[] args) where D : Delegate =>
        PrepareDelegate<D>(func, args.Select((a, i) => a.MakeTExArg(i)).ToArray());

    #endregion

}
[Reflect]
public static class Compilers {
    #region FallthroughCompilers
    
    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static TP TP(ExTP ex) => PrepareDelegateBPI<TP>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static SBV2 SBV2(ExTP ex) => PrepareDelegateRSB<SBV2>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static TP3 TP3(ExTP3 ex) => PrepareDelegateBPI<TP3>(ex).Compile();

    [DontReflect]
    [ExpressionBoundary]
    [Constable]
    public static TP3 TP3FromVec2(ExTP ex) => PrepareDelegateBPI<TP3>(tac => ExMV3.TP3(ex(tac))).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static TP4 TP4(ExTP4 ex) => PrepareDelegateBPI<TP4>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static VTP VTP(ExVTP ex) {
        if (ex == VTPRepo.ExNoVTP) return VTPRepo.NoVTP;
        return PrepareDelegate<VTP>(ex,
            new DelegateArg<Movement>("vtp_mov", true, true),
            new DelegateArg<float>("vtp_dt", true, true),
            new DelegateArg<ParametricInfo>("vtp_bpi", true, true),
            new DelegateArg<Vector3>("vtp_delta", true, true)
        ).Compile();
    }
    
    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static LVTP LVTP(ExVTP ex) =>
        PrepareDelegate<LVTP>(ex,
            new DelegateArg<LaserMovement>("vtp_mov", true, true),
            new DelegateArg<float>("vtp_dt", true),
            new DelegateArg<float>(LASER_TIME_ALIAS, true),
            new DelegateArg<ParametricInfo>("vtp_bpi", true, true),
            new DelegateArg<Vector3>("vtp_delta", true, true)
        ).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static FXY FXY(ExBPY ex) => PrepareDelegate<FXY>(ex, 
        MakeArg<float>("x", true)).Compile();
    
    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static Easer Easer(ExBPY ex) => PrepareDelegate<Easer>(ex, 
        MakeArg<float>("x", true)).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static BPY BPY(ExBPY ex) => PrepareDelegateBPI<BPY>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static SBF SBF(ExBPY ex) => PrepareDelegateRSB<SBF>(ex).Compile();
    

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static BPRV2 BPRV2(ExBPRV2 ex) => PrepareDelegateBPI<BPRV2>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static Pred Pred(ExPred ex) => PrepareDelegateBPI<Pred>(ex).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static LPred LPred(ExPred ex) => PrepareDelegate<LPred>(ex,
        new DelegateArg<ParametricInfo>("lpred_bpi", priority: true),
        new DelegateArg<float>(LASER_TIME_ALIAS)
    ).Compile();

    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static SBCF SBCF(ExSBCF ex) =>
        PrepareDelegate<SBCF>(tac => {
                var st = tac.GetByExprType<TExSBCUpdater>();
                var ct = tac.GetByExprType<TEx<ICancellee>>();
                return ex(st, ct, tac);
            },
            new DelegateArg<BulletManager.SimpleBulletCollection.VelocityUpdateState>("sbcf_updater", true),
            new DelegateArg<ParametricInfo>("sbcf_bpi", true),
            new DelegateArg<ICancellee>("sbcf_ct", true)
        ).Compile();

    public static readonly IDelegateArg[] GCXFArgs = {
        new DelegateArg<GenCtx>("gcx")
    };
    
    [Fallthrough]
    [ExpressionBoundary]
    [Constable]
    public static GCXF<T> GCXF<T>(Func<TExArgCtx, TEx<T>> ex) {
        return PrepareDelegate<GCXF<T>>(ex, GCXFArgs).Compile();
    }
    
    [ExpressionBoundary]
    [Constable]
    public static ErasedGCXF ErasedGCXF(Func<TExArgCtx, TEx> ex) {
        return PrepareDelegate<ErasedGCXF>(ex, GCXFArgs).Compile();
    }
    
    
    [ExpressionBoundary]
    [Constable]
    public static ErasedParametric ErasedParametric(Func<TExArgCtx, TEx> ex) => 
        PrepareDelegateBPI<ErasedParametric>(ex).Compile();

    #endregion


    /// <summary>
    /// Mark that some code should not be compiled in a script.
    /// </summary>
    [Constable]
    public static UncompiledCode<T> Code<T>(Func<TExArgCtx, TEx<T>> ex) => new(ex);
    
}

public interface IUncompiledCode {
    public Func<TExArgCtx, TEx> Code { get; }
}

/// <summary>
/// Code that has not yet been compiled in a script.
/// </summary>
/// <typeparam name="T"></typeparam>
public readonly struct UncompiledCode<T> : IUncompiledCode {
    public Func<TExArgCtx, TEx<T>> Code { get; }
    Func<TExArgCtx, TEx> IUncompiledCode.Code => Code;
    public UncompiledCode(Func<TExArgCtx, TEx<T>> code) {
        this.Code = code;
    }

    public static implicit operator UncompiledCode<T>(Func<TExArgCtx, TEx<T>> code) => new(code);

    public override string ToString() => $"Uncompiled<{typeof(T).RName()}>";
}
}
