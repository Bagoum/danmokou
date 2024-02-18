using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Reflection2;
using Danmokou.SM;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using UnityEngine;

namespace Danmokou.Reflection {
public static class SemanticTokenTypes {
    public const string Type = "type";
    public const string Method = "method";
    public const string Function = "function";
    public const string Operator = "dmkOperator";
    public const string Parameter = "parameter";
    public const string Variable = "variable";
    public const string EnumMember = "dmkEnumMember";
    public const string Keyword = "keyword";
    public const string String = "string";
    public const string Number = "number";

    public static readonly string[] Values = {
        Type, Method, Function, Operator, Parameter,
        Variable, EnumMember, Keyword, String, Number
    };
}

public class SemanticTokenModifiers {
    public const string Static = "static";
    public const string Deprecated = "deprecated";
    public const string Atomic = "dmkatomic";
    public const string Const = "const";
    
    //Method modifiers
    public const string SM = "dmksm";
    public const string AsyncP = "dmkasyncpattern";
    public const string SyncP = "dmksyncpattern";
    public const string VTP = "dmkvtp";
    public const string Control = "dmkcontrols";
    public const string Properties = "dmkproperties";
    public const string TP4 = "dmktp4";
    public const string TP3 = "dmktp3";
    public const string TP = "dmktp";
    public const string BPY = "dmkbpy";
    public const string BPRV2 = "dmkbprv2";
    public const string Pred = "dmkpred";

    public const string DynamicVar = "dmkdynamicvar";

    public static readonly string[] Values = {
        Static, Deprecated, Atomic, Const, SM, AsyncP, SyncP, VTP, Control,
        Properties, TP4, TP3, TP, BPY, BPRV2, Pred, DynamicVar
    };

    public static readonly Dictionary<string, Type[]> MethodModToTypes = new() {
        { SM, new[] { typeof(StateMachine), typeof(TaskPattern), typeof(TTaskPattern), typeof(ReflectableLASM) } },
        { AsyncP, new[] { typeof(AsyncPattern)} },
        { SyncP, new[] { typeof(SyncPattern)} },
        { VTP, new[] { typeof(VTP), typeof(LVTP), typeof(VTPExpr), typeof(LVTPExpr) } },
        { Control, new[] { typeof(BulletManager.exBulletControl), typeof(BulletManager.cBulletControl), typeof(BehaviorEntity.cBEHControl), typeof(CurvedTileRenderLaser.cLaserControl)} },
        { Properties, new[] { typeof(PatternProperty), typeof(PhaseProperty), typeof(GenCtxProperty), typeof(LaserOption), typeof(SBOption), typeof(BehOption) } },
        { TP4, new[] {typeof(TP4), typeof(GCXF<Vector4>), typeof(Vector4) } },
        { TP3, new[] { typeof(TP3), typeof(GCXF<Vector3>), typeof(Vector3) } },
        { TP, new[] {  typeof(TP), typeof(GCXF<Vector2>), typeof(Vector2) } },
        { BPY, new[] { typeof(BPY), typeof(FXY), typeof(GCXF<float>), typeof(float), typeof(int) } },
        { BPRV2, new[] { typeof(BPRV2), typeof(GCXF<V2RV2>), typeof(V2RV2) } },
        { Pred, new[] { typeof(Pred), typeof(GCXF<bool>), typeof(bool) } },
    };
    public static readonly Dictionary<Type, string> TypeToMethodMod = new();

    static SemanticTokenModifiers() {
        foreach (var (s, ts) in MethodModToTypes) {
            foreach (var t in ts)
                TypeToMethodMod[t] = s;
        }
    }

}

/// <summary>
/// A lexed parsing token that has a purpose.
/// </summary>
/// <param name="Position">Position of the token in the source code.</param>
/// <param name="TokenType">Type, from <see cref="SemanticTokenTypes"/></param>
/// <param name="TokenMods">Token modifiers, from <see cref="SemanticTokenModifiers"/></param>
[PublicAPI]
public record SemanticToken(PositionRange Position, string TokenType, IList<string>? TokenMods = null) {
    public SemanticToken WithConst(bool isConst) => isConst ?
        this with { TokenMods = (TokenMods ?? new string[0]).Append(SemanticTokenModifiers.Const).ToArray() } :
        this;

    public static string MethodType(IMethodSignature mi) {
        if (mi.GetAttribute<OperatorAttribute>() != null || mi.GetAttribute<BDSL2OperatorAttribute>() != null)
            return SemanticTokenTypes.Operator;
        if (mi.Member.Symbol == SymbolKind.Enum)
            return SemanticTokenTypes.EnumMember;
        return SemanticTokenTypes.Method;
    }
    public static SemanticToken FromMethod(IMethodSignature mi, PositionRange p, string? tokenType = null, Type? retType = null) {
        List<string>? mods = null;
        void AddMod(string? mod) {
            if (mod != null)
                (mods ??= new()).Add(mod);
        }
        void AddModIf(bool guard, string mod) {
            if (guard) AddMod(mod);
        }
        if ((tokenType ??= MethodType(mi)) == SemanticTokenTypes.Operator)
            return new(p, tokenType);
        AddModIf(mi.IsStatic, SemanticTokenModifiers.Static);
        AddModIf(mi.GetAttribute<ObsoleteAttribute>() != null, SemanticTokenModifiers.Deprecated);
        (retType ?? mi.ReturnType).IsTExOrTExFuncType(out var retTyp);
        if (SemanticTokenModifiers.TypeToMethodMod.TryGetValue(retTyp, out var v))
            AddMod(v);
        else if (mi.ReturnType.IsSubclassOf(typeof(StateMachine)))
            AddMod(SemanticTokenModifiers.SM);
        if (mi.GetAttribute<AtomicAttribute>() != null || mi.DeclaringType?.GetCustomAttribute<AtomicAttribute>() != null)
            AddMod(SemanticTokenModifiers.Atomic);
        return new(p, tokenType, mods);
    }
}
}