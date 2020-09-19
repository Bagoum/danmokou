using System;
using System.Collections.Generic;
using Danmaku;
using DMath;
using Core;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector4>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExLVTP = System.Func<Danmaku.ITExVelocity, RTEx<float>, RTEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExGCXF = System.Func<DMath.TExGCX, TEx>;
using ExSBF = System.Func<Danmaku.RTExSB, TEx<float>>;
using ExSBV2 = System.Func<Danmaku.RTExSB, TEx<UnityEngine.Vector2>>;
using ExSBCF = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx>;
using ExSBPred = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx<bool>>;

public static class Compilers {
    #region RawCompilers
    private static D CompileDelegateLambda<D, ExT1>(Func<ExT1, TEx> exConstructor) where ExT1 : TEx, new() {
        ExT1 t1 = new ExT1();
        return Ex.Lambda<D>(exConstructor(t1).Flatten(), t1).Compile();
    }

    private static D CompileDelegateLambda<D, ExT1, ExT2>(Func<ExT1, ExT2, TEx> exConstructor)
        where ExT1 : TEx, new()
        where ExT2 : TEx, new() {
        ExT1 t1 = new ExT1();
        ExT2 t2 = new ExT2();
        return Ex.Lambda<D>(exConstructor(t1, t2).Flatten(), t1, t2).Compile();
    }
    
    private static D CompileDelegateLambda<D, ExT1, ExT2, ExT3>(Func<ExT1, ExT2, ExT3, TEx> exConstructor)
        where ExT1 : TEx, new()
        where ExT2 : TEx, new()
        where ExT3 : TEx, new() {
        ExT1 t1 = new ExT1();
        ExT2 t2 = new ExT2();
        ExT3 t3 = new ExT3();
        return Ex.Lambda<D>(exConstructor(t1, t2, t3).Flatten(), t1, t2, t3).Compile();
    }

    private static D CompileDelegateLambda<D, ExT1, ExT2, ExT3, ExT4>(Func<ExT1, ExT2, ExT3, ExT4, TEx> exConstructor)
        where ExT1 : TEx, new()
        where ExT2 : TEx, new()
        where ExT3 : TEx, new()
        where ExT4 : TEx, new() {
        ExT1 t1 = new ExT1();
        ExT2 t2 = new ExT2();
        ExT3 t3 = new ExT3();
        ExT4 t4 = new ExT4();
        return Ex.Lambda<D>(exConstructor(t1, t2, t3, t4).Flatten(), t1, t2, t3, t4).Compile();
    }

    private static D CompileDelegateLambda<D, ExT1, ExT2, ExT3, ExT4, ExT5>(Func<ExT1, ExT2, ExT3, ExT4, ExT5, TEx> exConstructor)
        where ExT1 : TEx, new()
        where ExT2 : TEx, new()
        where ExT3 : TEx, new()
        where ExT4 : TEx, new()
        where ExT5 : TEx, new() {
        ExT1 t1 = new ExT1();
        ExT2 t2 = new ExT2();
        ExT3 t3 = new ExT3();
        ExT4 t4 = new ExT4();
        ExT5 t5 = new ExT5();
        return Ex.Lambda<D>(exConstructor(t1, t2, t3, t4, t5).Flatten(), t1, t2, t3, t4, t5).Compile();
    }
    #endregion

    [Fallthrough] [ExprCompiler]
    public static TP TP(ExTP ex) => CompileDelegateLambda<TP, TExPI>(ex);
    [Fallthrough] [ExprCompiler]
    public static SBV2 SBV2(ExSBV2 ex) => CompileDelegateLambda<SBV2, RTExSB>(ex);
    [Fallthrough] [ExprCompiler]
    public static TP3 TP3(ExTP3 ex) => CompileDelegateLambda<TP3, TExPI>(ex);
    [Fallthrough] [ExprCompiler]
    public static TP4 TP4(ExTP4 ex) => CompileDelegateLambda<TP4, TExPI>(ex);
    
    public static VTP VTP_Force(ExVTP ex) => CompileDelegateLambda<VTP, RTExVel, RTEx<float>, TExPI, RTExV2>(ex);
    [Fallthrough] [ExprCompiler]
    public static VTP VTP(ExVTP ex) {
        if (ex == VTPRepo.ExNoVTP) return VTPRepo.NoVTP;
        return VTP_Force(ex);
    }


    public const string LASER_TIME_ALIAS = "lt";

    [Fallthrough] [ExprCompiler]
    public static ExLVTP ExLVTP(ExVTP vtp) => (vel, dt, lt, bpi, nrv) => {
        using (new ReflectEx.LetDirect(LASER_TIME_ALIAS, lt)) {
            return vtp(vel, dt, bpi, nrv);
        }
    };
    
    [Fallthrough] [ExprCompiler]
    public static LVTP LVTP(ExLVTP ex) => CompileDelegateLambda<LVTP, RTExLVel, RTEx<float>, RTEx<float>, TExPI, RTExV2>(ex);
    private static LVTP _LVTP(ExVTP ex) => CompileDelegateLambda<LVTP, RTExLVel, RTEx<float>, RTEx<float>, TExPI, RTExV2>(ExLVTP(ex));

    [Fallthrough] [ExprCompiler]
    public static FXY FXY(ExFXY ex) => CompileDelegateLambda<FXY, TEx<float>>(ex);
    [Fallthrough] [ExprCompiler]
    public static BPY BPY(ExBPY ex) => CompileDelegateLambda<BPY, TExPI>(ex);
    [Fallthrough] [ExprCompiler]
    public static SBF SBF(ExSBF ex) => CompileDelegateLambda<SBF, RTExSB>(ex);

    [Fallthrough] [ExprCompiler]
    public static LBPY LBPY(ExBPY ex) => CompileDelegateLambda<LBPY, TExPI, TEx<float>>((pi, lt) => {
        using (new ReflectEx.LetDirect(LASER_TIME_ALIAS, lt)) {
            return ex(pi);
        }
    });
    
    [Fallthrough] [ExprCompiler]
    public static BPRV2 BPRV2(ExBPRV2 ex) => CompileDelegateLambda<BPRV2, TExPI>(ex);
    [Fallthrough] [ExprCompiler]
    public static Pred Pred(ExPred ex) => CompileDelegateLambda<Pred, TExPI>(ex);
    [Fallthrough] [ExprCompiler]
    public static LPred LPred(ExPred ex) => CompileDelegateLambda<LPred, TExPI, TEx<float>>((pi, lt) => {
        using (new ReflectEx.LetDirect(LASER_TIME_ALIAS, lt)) {
            return ex(pi);
        }
    });
    [Fallthrough] [ExprCompiler]
    public static SBCF SBCF(ExSBCF ex) => CompileDelegateLambda<SBCF, TExSBC, TEx<int>, TExPI>(ex);

    //requires manual handling for wrapper
    private static GCXF<T> GCXF<T>(ExGCXF ex) {
        TExGCX t1 = new TExGCX();
        ReflectEx.SetGCX(t1);
        var gcxf = Ex.Lambda<GCXF<T>>(ex(t1).Flatten(), t1).Compile();
        ReflectEx.RemoveGCX();
        return gcxf;
    }

    [Fallthrough] [ExprCompiler]
    public static GCXF<bool> GCXF(ExPred f) => GCXF<bool>(GCXFRepo.GCX_Pred(f));
    [Fallthrough] [ExprCompiler]
    public static GCXF<float> GCXF(ExBPY f) => GCXF<float>(GCXFRepo.GCX_BPY(f));

    public static GCXF<float> GCXFf(ExBPY f) => GCXF(f);
    [Fallthrough] [ExprCompiler]
    public static GCXF<Vector2> GCXF(ExTP f) => GCXF<Vector2>(GCXFRepo.GCX_TP(f));
    [Fallthrough] [ExprCompiler]
    public static GCXF<Vector3> GCXF(ExTP3 f) => GCXF<Vector3>(GCXFRepo.GCX_TP3(f));
    [Fallthrough] [ExprCompiler]
    public static GCXF<Vector4> GCXF(ExTP4 f) => GCXF<Vector4>(GCXFRepo.GCX_TP4(f));
    [Fallthrough] [ExprCompiler]
    public static GCXF<V2RV2> GCXF(ExBPRV2 f) => GCXF<V2RV2>(GCXFRepo.GCX_BPRV2(f));

    public static GCXF<V2RV2> GCXFrv2(ExBPRV2 f) => GCXF(f);

    private static GCXU<T2> GCXU11<T1, T2>(Func<Func<TExPI, TEx<T1>>, T2> compiler, Func<TExPI, TEx<T1>> f) =>
        Automatic(compiler, f, aliases => bpi => ReflectEx.Let2(aliases, () => f(bpi), bpi));
    private static GCXU<T2> GCXUsb11<T1, T2>(Func<Func<RTExSB, TEx<T1>>, T2> compiler, Func<RTExSB, TEx<T1>> f) =>
        Automatic(compiler, f, aliases => sb => ReflectEx.Let2(aliases, () => f(sb), sb.bpi));
    [Fallthrough] [ExprCompiler]
    public static GCXU<BPY> GCXU(ExBPY f) => GCXU11(BPY, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<FnLaserV4> GCXULaser(ExTP4 f) => GCXU11(FnLaserFloat, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<Pred> GCXU(ExPred f) => GCXU11(Pred, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<TP> GCXU(ExTP f) => GCXU11(TP, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<TP3> GCXU(ExTP3 f) => GCXU11(TP3, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<TP4> GCXU(ExTP4 f) => GCXU11(TP4, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<BPRV2> GCXU(ExBPRV2 f) => GCXU11(BPRV2, f);
    [Fallthrough] [ExprCompiler]
    public static GCXU<SBV2> GCXU(ExSBV2 f) => GCXUsb11(SBV2, f);

    [Fallthrough] [ExprCompiler]
    public static GCXU<VTP> GCXU(ExVTP f) => Automatic(VTP, f, aliases => VTPRepo.LetDecl(aliases, f));
    
    [Fallthrough] [ExprCompiler]
    public static GCXU<LVTP> LGCXU(ExVTP f) => Automatic(_LVTP, f, aliases => VTPRepo.LetDecl(aliases, f));
    
    [Fallthrough] [ExprCompiler]
    public static FnLaserV4 FnLaserFloat(ExTP4 ex) {
        var texpi = new TExPI();
        ReflectEx.aliased_laser = new TEx<CurvedTileRenderLaser>();
        var result = Ex.Lambda<FnLaserV4>(ex(texpi).Flatten(), texpi, ReflectEx.aliased_laser).Compile();
        ReflectEx.aliased_laser = null;
        return result;
    }

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

    private static GCXU<T> Automatic<S, T>(Func<S, T> compiler, S exp, Func<ReflectEx.Alias<TExPI>[], S> relet) {
        var resolver = new GCXCompileResolver();
        ReflectEx.SetICRR(resolver);
        var p = compiler(exp);
        ReflectEx.RemoveICRR();
        if (resolver.bound.Count > 0) { //Automatic resolver found something, recompile
            return Expose(resolver.bound.ToArray(), compiler, exp, relet);
        }
        var bound = new (Reflector.ExType, string)[0];
        return new GCXU<T>(
            (GenCtx gcx, ref uint id) => {
                PrivateDataHoisting.UploadNew(bound, gcx, ref id);
                return p;
            }, (gcx, id) => {
                PrivateDataHoisting.UploadAdd(bound, gcx, id);
                return p;
            });
    }
    private static GCXU<T> Expose<S, T>((Reflector.ExType, string)[] exportVars, Func<S, T> compiler, S exp, Func<ReflectEx.Alias<TExPI>[], S> relet) {
        var aliases = new ReflectEx.Alias<TExPI>[exportVars.Length];
        for (int ii = 0; ii < exportVars.Length; ++ii) {
            var (ext, boundVar) = exportVars[ii];
            //The "better" way to do this would be to copy the GCX values into the let statements
            //and recompile the expression for every caller.
            //However, this is ridiculously expensive, so instead we HOIST.
            aliases[ii] = new ReflectEx.Alias<TExPI>(boundVar, PrivateDataHoisting.GetValue(ext, boundVar));
        }
        var p = compiler((aliases.Length > 0) ? relet(aliases) : exp);
        return new GCXU<T>(
            (GenCtx gcx, ref uint id) => {
                PrivateDataHoisting.UploadNew(exportVars, gcx, ref id);
                return p;
            }, (gcx, id) => {
                PrivateDataHoisting.UploadAdd(exportVars, gcx, id);
                return p;
            });
    }
    
}