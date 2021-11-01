using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.SM;
using Danmokou.SM.Parsing;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector4>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx>;
using ExSBCF = System.Func<Danmokou.Expressions.TExSBC, Danmokou.Expressions.TEx<int>, Danmokou.Expressions.TEx<BagoumLib.Cancellation.ICancellee>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx>;

namespace Danmokou.Reflection {
public static partial class Reflector {
    private static readonly Dictionary<Type, string> TypeNameMap = new Dictionary<Type, string>() {
        {typeof(float), "Float"},
        {typeof(ExBPY), "BPY"},
        {typeof(ExTP), "TP"},
        {typeof(ExTP3), "TP3"},
        {typeof(ExTP4), "TP4"},
        {typeof(ExVTP), "VTP"},
        {typeof(ExBPRV2), "BPRV2"},
        {typeof(ExPred), "Predicate"},
        {typeof(ExSBCF), "SB Control"},
    };
    private static readonly Type tsm = typeof(StateMachine);

    private static StateMachine? ReflectSM(IParseQueue q) {
        string method = q.Scan();
        if (method == "file") {
            q.Advance();
            return StateMachineManager.FromName(q.Next());
        } else if (method == "null") {
            q.Advance();
            return null;
        } else if (method == "stall") {
            q.Advance();
            return WaitForPhaseSM;
        } else {
            var line = q.GetLastLine();
            try {
                return StateMachine.Create(q);
            } catch (Exception ex) {
                throw new SMException($"Nested StateMachine construction starting on line {line} failed.", ex);
            }
        }
    }

    public static readonly StateMachine WaitForPhaseSM =
        new ReflectableLASM(SMReflection.Wait(Synchronization.Time(_ => M.IntFloatMax)));

    /// <summary>
    /// Maps types to a function that parses that type from a single word.
    /// </summary>
    private static readonly Dictionary<Type, Func<string, object?>> SimpleFunctionResolver =
        new Dictionary<Type, Func<string, object?>>() {
            {typeof(float), arg => Parser.Float(arg)},
            {typeof(V2RV2), arg => Parser.ParseV2RV2(arg)},
            {typeof(CRect), arg => Parser.ParseRect(arg)},
            {typeof(CCircle), arg => Parser.ParseCircle(arg)},
            {typeof(BEHPointer), BehaviorEntity.GetPointerForID},
            {typeof(ETime.Timer), ETime.Timer.GetTimer},
        };


    private static readonly Type tint = typeof(int);
    private static readonly Type type_stylesel = typeof(BulletManager.StyleSelector);
    private static readonly Type type_alias = typeof(ReflectEx.Alias);
    private static readonly Type type_gcrule = typeof(GCRule);
    private static readonly Type gtype_ienum = typeof(IEnumerable<>);
    private static readonly Type type_locstring = typeof(LString);


    private static bool MatchesGeneric(Type target, Type generic) =>
        target.IsConstructedGenericType && target.GetGenericTypeDefinition() == generic;

    /// <summary>
    /// A cached dictionary of constructor signatures from GetConstructorSignature.
    /// </summary>
    private static readonly Dictionary<Type, NamedParam[]> constructorSigs = new Dictionary<Type, NamedParam[]>();
    
    /// <summary>
    /// Finds a public constructor (preferably one with at least one argument) for the given type.
    /// </summary>
    /// <exception cref="StaticException">Thrown if the type has no public constructors.</exception>
    public static NamedParam[] GetConstructorSignature(Type t) {
        if (!constructorSigs.TryGetValue(t, out NamedParam[] args)) {
            var constrs = t.GetConstructors();
            if (constrs.Length == 0) 
                throw new StaticException($"Type {NameType(t)} has no constructors.");
            var prms = constrs[0].GetParameters();
            if (prms.Length == 0) {
                //Try to look for a non-empty constructor, if it exists.
                for (int ii = 1; ii < constrs.Length; ++ii) {
                    if (constrs[ii].GetParameters().Length > 0) prms = constrs[ii].GetParameters();
                }
            }
            constructorSigs[t] = args = prms.Select(x => (NamedParam) x).ToArray();
        }
        return args;
    }


    private static bool ResolveSpecialHandling(IParseQueue p, Type targetType, out object obj) {
        if (targetType == type_locstring) {
            var str = p.Next();
            obj = LocalizedStrings.IsLocalizedStringReference(str) ? 
                LocalizedStrings.TryFindReference(str) ?? 
                    throw new Exception($"Line {p.GetLastLine()}: Couldn't resolve LocalizedString {str}")
                : new LString(str);
        } else if (targetType == type_stylesel) {
            obj = new BulletManager.StyleSelector((ResolveAsArray(typeof(string[]), p) as string[][])!);
        } else if (targetType == type_gcrule) {
            ReferenceMember rfr = new ReferenceMember(p.Next());
            string OpAndMaybeType = p.Next();
            var op = (GCOperator) ForceFuncTypeResolve(OpAndMaybeType, typeof(GCOperator))!;
            var latter = OpAndMaybeType.Split('=').Try(1) ?? throw new ParsingException(
                $"Line {p.GetLastLine()}: Trying to parse GCRule, but found an invalid operator {OpAndMaybeType}.\n" +
                $"Make sure to put parentheses around the right-hand side of GCRule.");
            var ext = (ExType) ForceFuncTypeResolve(latter.Length > 0 ? latter : p.Next(), typeof(ExType))!;
            if (ext == ExType.Float) obj = new GCRule<float>(ext, rfr, op, p.Into<GCXF<float>>());
            else if (ext == ExType.V2) obj = new GCRule<Vector2>(ext, rfr, op, p.Into<GCXF<Vector2>>());
            else if (ext == ExType.V3) obj = new GCRule<Vector3>(ext, rfr, op, p.Into<GCXF<Vector3>>());
            else if (ext == ExType.RV2) obj = new GCRule<V2RV2>(ext, rfr, op, p.Into<GCXF<V2RV2>>());
            else throw new StaticException($"No GCRule handling for ExType {ext}");
        } else if (targetType == type_alias) {
            ExType declTyp = (ExType) ForceFuncTypeResolve(p.Next(), typeof(ExType))!;
            string alias = p.Next();
            var req_type = typeof(Func<,>).MakeGenericType(typeof(TExArgCtx), AsWeakTExType(declTyp));
            obj = Activator.CreateInstance(targetType, alias, ReflectTargetType(p, req_type));
        } else if (UseConstructor(targetType)) {
            //generic struct/tuple handling
            var args = GetConstructorSignature(targetType);
            obj = Activator.CreateInstance(targetType, _FillInvokeArray(args, p, targetType, null));
        } else {
            obj = default!;
            return false;
        }
        return true;
    }

    public static bool UseConstructor(Type t) => classAutoReflectTypes.Contains(t) ||
                                                 (t.IsValueType && !t.IsPrimitive && !t.IsEnum);

    private static readonly HashSet<Type> classAutoReflectTypes = new HashSet<Type>() {
        typeof(GenCtxProperties<SyncPattern>),
        typeof(GenCtxProperties<AsyncPattern>),
        typeof(GenCtxProperties<StateMachine>),
        typeof(SBOptions),
        typeof(LaserOptions),
        typeof(BehOptions),
        typeof(PowerAuraOptions),
        typeof(PatternProperties)
    };

    private static bool FuncTypeResolve(string s, Type targetType, out object? result) {
        if (SimpleFunctionResolver.TryGetValue(targetType, out Func<string, object?> resolver)) {
            try {
                result = resolver(s);
                return true;
            } catch (Exception) {
                // ignored
            }
        }
        result = default!;
        return false;
    }

    private static object? ForceFuncTypeResolve(string s, Type targetType) {
        if (SimpleFunctionResolver.TryGetValue(targetType, out Func<string, object?> resolver)) {
            return resolver(s);
        } else throw new StaticException("ForceFuncTypeResolve was used for a type without a simple resolver");
    }

    public static object ResolveAsArray(Type eleType, IParseQueue q) {
        if (eleType == null) throw new StaticException($"Requested an array of null elements");
        if (IParseQueue.ARR_EMPTY.Contains(q.MaybeScan() ?? "")) {
            q.Advance();
            var empty = Array.CreateInstance(eleType, 0);
            return empty;
        }
        if (q.MaybeScan() != IParseQueue.ARR_OPEN) {
            var singleton = Array.CreateInstance(eleType, 1);
            singleton.SetValue(_ReflectTargetType(q, eleType), 0);
            return singleton;
        }
        q.Advance(); // {
        var arr = new List<object?>();
        while (q.MaybeScan() != IParseQueue.ARR_CLOSE) {
            arr.Add(_ReflectTargetType(q.NextChild(), eleType));
        }
        q.Advance(); // }
        var arr_true = Array.CreateInstance(eleType, arr.Count);
        for (int ii = 0; ii < arr_true.Length; ++ii) {
            arr_true.SetValue(arr[ii], ii);
        }
        return arr_true;
    }

    [UsedImplicitly]
    public static object[] TupleToArr2<T1, T2>((T1, T2) tup) => new object[] {tup.Item1!, tup.Item2!};
}
}

