using System;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Mizuhashi;
using Scriptor;
using Scriptor.Analysis;

namespace Danmokou.Reflection {
public enum AutoVarMethod: int {
    None,
    GenCtx
}

public enum AutoVarExtend: int {
    BindItr,
    BindAngle,
    BindLR,
    BindUD,
    BindArrow
}


public static class AutoVarHelper {
    private static VarDecl Declare<T>(LexicalScope s, PositionRange p, string name, string comment) => 
        s.DeclareOrThrow(new VarDecl(p, false, typeof(T), name) {
            DocComment = comment
        });
    
    public static void AutoDeclare(LexicalScope s, PositionRange p, AutoVarMethod method) {
        s.SetAutovars(method switch {
            AutoVarMethod.GenCtx => new AutoVars.GenCtx(
                Declare<float>(s, p, "i", "Iteration index (starting at 0) of this repeater"),
                Declare<float>(s, p, "pi", "Iteration index (starting at 0) of the parent repeater"),
                Declare<V2RV2>(s, p, "rv2", "Rotational coordinates"),
                Declare<V2RV2>(s, p, "brv2", "Rotational coordinates provided by parent function"),
                Declare<float>(s, p, "st",
                    "Summon Time - the time in all repeaters that has passed since the last TimeReset GCXProp"),
                Declare<float>(s, p, "times", "Number of times this repeater will execute for"),
                Declare<float>(s, p, "ir", "Iteration ratio = i / (times - 1)")),
            _ => new AutoVars.None()
        });
    }

    public static void AutoExtend(LexicalScope s, PositionRange p, AutoVarExtend ext, string? key = null) {
        if (s.AutoVars is not {} av)
            throw new StaticException("AutoVars not yet declared in this scope");
        if (av is not AutoVars.GenCtx gcxAV)
            throw new Exception($"Cannot extend {ext} on non-GCX autovars");
        switch (ext) {
            case AutoVarExtend.BindAngle:
                gcxAV.bindAngle = Declare<float>(s, p, "angle", "The value copied from rv2.a");
                break;
            case AutoVarExtend.BindItr:
                gcxAV.bindItr = Declare<float>(s, p, key ?? throw new StaticException("Target not provided for bindItr"), 
                    "The value copied from i (iteration index)");
                break;
            case AutoVarExtend.BindLR:
                gcxAV.bindLR = (Declare<float>(s, p, "lr", "1 if the iteration index is even, -1 if it is odd"), 
                    Declare<float>(s, p, "rl", "-1 if the iteration index is even, 1 if it is odd"));
                break;
            case AutoVarExtend.BindUD:
                gcxAV.bindUD = (Declare<float>(s, p, "ud", "1 if the iteration index is even, -1 if it is odd"), 
                    Declare<float>(s, p, "du", "-1 if the iteration index is even, 1 if it is odd"));
                break;
            case AutoVarExtend.BindArrow:
                gcxAV.bindArrow = (Declare<float>(s, p, "axd", "BindArrow axd"), Declare<float>(s, p, "ayd", "BindArrow ayd"), 
                    Declare<float>(s, p, "aixd", "BindArrow aixd"), Declare<float>(s, p, "aiyd", "BindArrow aiyd"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(ext), ext, null);
        }

    }
}

public record AutoVars : IAutoVars {
    public record None : AutoVars;

    /// <summary>
    /// </summary>
    /// <param name="i">Loop iteration</param>
    /// <param name="pi">Parent loop iteration</param>
    /// <param name="rv2">Current rotation</param>
    /// <param name="brv2">Base rotation provided by parent</param>
    /// <param name="st">Summon time</param>
    /// <param name="times">Maximum number of loops</param>
    /// <param name="ir">Loop ratio i/(times-1)</param>
    public record GenCtx(VarDecl i, VarDecl pi, VarDecl rv2, VarDecl brv2, VarDecl st, VarDecl times, VarDecl ir) : AutoVars {
        public VarDecl? bindItr;
        public VarDecl? bindAngle;
        public (VarDecl lr, VarDecl rl)? bindLR;
        public (VarDecl ud, VarDecl du)? bindUD;
        public (VarDecl axd, VarDecl ayd, VarDecl aixd, VarDecl aiyd)? bindArrow;
    }
    
    public override string ToString() => "-Scope Autovars-";
}
}