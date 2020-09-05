using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using static DMath.ExMHelpers;
using static ExUtils;
using static DMath.EaseHelpers;
using static DMath.FXYRepo;
using static DMath.ExM;

namespace DMath {
public static class EaseHelpers {
    private static readonly Dictionary<string, ExFXY> funcs = new Dictionary<string, ExFXY>() {
        { "linear", ELinear },
        { "in-quad" , EInQuad },
        { "in-sine", EInSine },
        { "out-sine", EOutSine },
        { "io-sine", EIOSine },
        { "in-hsine", t => SuperposeC(E05, ELinear(t), EInSine(t)) },
        { "out-hsine", t => SuperposeC(E05, ELinear(t), EOutSine(t)) },
        { "io-hsine", t => SuperposeC(E05, ELinear(t), EIOSine(t)) },
        { "sine-010", ESine010 },
        { "smod-010", ESoftmod010 },
        { "bounce2", EBounce2 }
    };
    private static readonly Dictionary<string, FXY> cfuncs = new Dictionary<string, FXY>() {
        { "linear", x => x }
    };
    public static string[] Functions => funcs.Keys.ToArray();

    private static bool TryGetOrCacheFXY(string name, out FXY fxy) {
        if (cfuncs.TryGetValue(name, out fxy)) return true;
        if (funcs.TryGetValue(name, out var ex)) {
            fxy = cfuncs[name] = Compilers.FXY(ex);
            return true;
        }
        return false;
    }

    public static ExFXY GetFunc(string name) => funcs.GetOrThrow(name, "easing functions");
    public static ExFXY GetDeriv(string name) => x => 
        DerivativeVisitor.Derivate(x, E1, funcs.GetOrThrow(name, "easing functions (from derivative call)")(x));
    
    public static FXY GetFuncOrRemoteOrLinear(string name) {
        FXY f;
#if !NO_EXPR
        if (TryGetOrCacheFXY(name, out f)) return f;
#endif
        if (EasingFunctionRemote.easingMethodStrAccess.ContainsKey(name)) {
            Func<float, float, float, float> ffunc =
                EasingFunctionRemote.GetEasingFunction(EasingFunctionRemote.easingMethodStrAccess[name]);
            return t => ffunc(0, 1, t);
        }
        TryGetOrCacheFXY("linear", out f);
        return f;
    }
}

}