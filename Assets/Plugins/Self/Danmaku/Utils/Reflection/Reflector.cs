using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Danmaku;
using DMath;
using Core;
using JetBrains.Annotations;
using SM;
using Ex = System.Linq.Expressions.Expression;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
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

public static partial class Reflector {
    private static readonly Dictionary<Type, string> TypeNameMap = new Dictionary<Type, string>() {
        {typeof(float), "Float"},
        {typeof(ExFXY), "FXY"},
        {typeof(ExBPY), "BPY"},
        {typeof(ExTP), "TP"},
        {typeof(ExTP3), "TP3"},
        {typeof(ExBPRV2), "BPRV2"},
        {typeof(ExPred), "Predicate"},
        {typeof(ExSBPred), "SB Predicate"},
        {typeof(ExSBCF), "SB Control"},
        {typeof(ExSBV2), "SB>V2 Func"},
        {typeof(ExSBF), "SB>F Func"},
    };
    private static readonly Type tsm = typeof(StateMachine);

    [CanBeNull]
    private static StateMachine ReflectSM(ParsingQueue q) {
        string method = q.Scan(out int index);
        if (method == "file") {
            q.Next();
            return StateMachineManager.FromName(q.Next());
        } else if (method == "null") {
            q.Next();
            return null;
        }
        else if (method == "wait" || method == "wait-phase") {
            q.Next();
            return WaitForPhaseSM;
        } else {
            if (method == "here") q.Next();
            try {
                return StateMachine.Create(q);
            } catch (Exception ex) {
                throw new SMException($"Nested StateMachine construction starting on line {q.GetLastLine(index)} failed.", ex);
            }
        }
    }

    public static readonly StateMachine WaitForPhaseSM = new ReflectableLASM(SMReflection.Wait(Synchronization.Time(_ => M.IntFloatMax)));
    
    private static readonly Dictionary<Type, Func<string, object>> SimpleFunctionResolver = new Dictionary<Type, Func<string, object>>() {
        {typeof(Events.Event0), Events.Event0.Find},
        {typeof(Maybe<Events.Event0>), arg => Events.Event0.FindOrNull(arg)},
        {typeof(float), arg => Parser.Float(arg)},
        {typeof(V2RV2), arg => Parser.ParseV2RV2(arg)},
        {typeof(CRect), arg => Parser.ParseRect(arg)},
        {typeof(CCircle), arg => Parser.ParseCircle(arg)},
        {typeof(MovementModifiers), arg => Parser.ParseMovementModifiers(arg)},
        {typeof(BEHPointer), BehaviorEntity.GetPointerForID},
        {typeof(ETime.Timer), ETime.Timer.GetTimer},
    };
    


    private static readonly Type tint = typeof(int);
    private static readonly Dictionary<Type, Type[]> constructorSigs = new Dictionary<Type, Type[]>();

    private static readonly Type type_stylesel = typeof(BulletManager.StyleSelector);
    private static readonly Type type_stringAA = typeof(string[][]);
    private static readonly Type gtype_alias = typeof(ReflectEx.Alias<>);
    private static readonly Type type_gcrule = typeof(GCRule);
    private static readonly Type gtype_ienum = typeof(IEnumerable<>);
    

    private static bool MatchesGeneric(Type target, Type generic) =>
        target.IsConstructedGenericType && target.GetGenericTypeDefinition() == generic;
    public static Type[] GetConstructorSignature(Type t) {
        if (!constructorSigs.TryGetValue(t, out Type[] args)) {
            var constrs = t.GetConstructors();
            if (constrs.Length == 0) throw new StaticException($"Type {NameType(t)} has no constructors.");
            var prms = constrs[0].GetParameters();
            if (prms.Length == 0) { //Try to look for a non-empty constructor, if it exists.
                for (int ii = 1; ii < constrs.Length; ++ii) {
                    if (constrs[ii].GetParameters().Length > 0) prms = constrs[ii].GetParameters();
                }
            }
            constructorSigs[t] = args = new Type[prms.Length];
            for (int ii = 0; ii < args.Length; ++ii) args[ii] = prms[ii].ParameterType;
        }
        return args;
    }

    private static readonly Type tpsmp = typeof(PhaseProperties);


    private static bool ResolveSimple(ParsingQueue p, Type targetType, out object obj) {
        if (targetType == type_stylesel) {
            obj = new BulletManager.StyleSelector(CastToType(p, type_stringAA) as string[][]);
        } else if (targetType == type_gcrule) {
            ReferenceMember rfr = new ReferenceMember(p.Next());
            string OpAndMaybeType = p.Next();
            FuncTypeResolve(OpAndMaybeType, typeof(GCOperator), out var _op);
            var latter = OpAndMaybeType.Split('=')[1];
            object _ext;
            if (latter.Length > 0) {
                FuncTypeResolve(latter, typeof(ExType), out _ext);
            } else _ext = CastToType(p, typeof(ExType));
            var ext = (ExType) _ext;
            var op = (GCOperator) _op;
            if (ext == ExType.Float) obj = new GCRule<float>(ext, rfr, op, p.Into<GCXF<float>>());
            else if (ext == ExType.V2) obj = new GCRule<Vector2>(ext, rfr, op, p.Into<GCXF<Vector2>>());
            else if (ext == ExType.V3) obj = new GCRule<Vector3>(ext, rfr, op, p.Into<GCXF<Vector3>>());
            else if (ext == ExType.RV2) obj = new GCRule<V2RV2>(ext, rfr, op, p.Into<GCXF<V2RV2>>());
            else throw new StaticException($"No GCRule handling for ExType {ext}");
        } else if (MatchesGeneric(targetType, gtype_alias)) {
            ExType declTyp = (ExType) CastToType(p, typeof(ExType));
            string alias = p.Next();
            var req_type = typeof(Func<,>).MakeGenericType(targetType.GenericTypeArguments[0], AsWeakTExType(declTyp));
            obj = Activator.CreateInstance(targetType, alias, ReflectTargetType(p, req_type));
        } else if (UseConstructor(targetType)) {
            //generic struct/tuple handling
            var args = GetConstructorSignature(targetType);
            obj = Activator.CreateInstance(targetType, _FillInvokeArray(args, p, targetType, null));
        } else {
            obj = default;
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
        typeof(PatternProperties)
    };

    private static bool FuncTypeResolve(string s, Type targetType, out object result) {
        if (SimpleFunctionResolver.TryGetValue(targetType, out Func<string, object> resolver)) {
            result = resolver(s);
            return true;
        }
        result = default;
        return false;
    }

    public static object ResolveAsArray(Func<ParsingQueue, Type, object> resolve1, Type eleType, ParsingQueue p) {
        if (eleType == null) throw new StaticException($"Requested an array of null elements");
        if (ParsingQueue.ARR_EMPTY.Contains(p.Scan())) {
            p.Next();
            var empty = Array.CreateInstance(eleType, 0);
            return empty;
        }
        if (p.Scan() != ParsingQueue.ARR_OPEN) {
            var singleton = Array.CreateInstance(eleType, 1);
            singleton.SetValue(resolve1(p, eleType), 0);
            return singleton;
        }
        p.Next(); // {
        var arr = new List<object>();
        while (p.Scan() != ParsingQueue.ARR_CLOSE) {
            arr.Add(resolve1(p, eleType));
        }
        p.Next(); // }
        var arr_true = Array.CreateInstance(eleType, arr.Count);
        for (int ii = 0; ii < arr_true.Length; ++ii) {
            arr_true.SetValue(arr[ii], ii);
        }
        return arr_true;
    }

    private static object ResolveAsArray(Type eleType, ParsingQueue p) =>
        ResolveAsArray(_ReflectTargetType, eleType, p);
    private static object CastToType(ParsingQueue p, Type targetType) {
        if (FuncTypeResolve(p.Scan(), targetType, out object result)) { p.Next(); return result; }
        if (ResolveSimple(p, targetType, out object obj)) return obj;
        if (targetType.IsArray) return ResolveAsArray(targetType.GetElementType(), p);
        if (MatchesGeneric(targetType, gtype_ienum)) return ResolveAsArray(targetType.GenericTypeArguments[0], p);
        return CastToType(p.Next(out int index), targetType, p.GetLastLine(index));
    }
    private static object CastToType(string arg, Type targetType, int lineIndex) {
        if (arg == "_") {
            // Max value shortcut for eg. repeating until cancel
            if (targetType == tint) return M.IntFloatMax;
        }
        try {
            return Convert.ChangeType(arg, targetType);
        } catch (Exception) {
            throw new BadTypeException($"Line {lineIndex}: Couldn't auto-convert \"{arg}\" to type {NameType(targetType)}.");
        }
    }
    [UsedImplicitly]
    public static object[] TupleToArr2<T1, T2>((T1, T2) tup) => new object[] {tup.Item1, tup.Item2};
}

