using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.Expressions {

/// <summary>
/// An arbitrary set of arguments to an expression function.
/// <br/>Expression functions are written in the general form Func&lt;TExArgCtx, TEx&lt;R&gt;&gt;
///  and compiled to Func&lt;T1, T2..., R&gt;, where T1,T2... are types that have stored their information
///  in TExArgCtx (usually <see cref="ParametricInfo"/> or <see cref="BulletManager.SimpleBullet"/>),
///  and R is some standard return type like float or Vector2.
/// </summary>
public class TExArgCtx {
    /// <summary>
    /// Context that is shared by any copies of this.
    /// </summary>
    public class RootCtx {
        /// <summary>
        /// A handler that tracks usages of yet-unbound variables in the precompilation step of GCXU.
        /// <br/>This is *NOT* used for any actual compilation.
        /// <br/>It is set to <see cref="CompilerHelpers.GCXUCompileResolver"/> in the first phase of GCXU compilation,
        ///  and <see cref="CompilerHelpers.GCXUDummyResolver"/> in the second phase of GCXU compilation.
        /// </summary>
        public ReflectEx.ICompileReferenceResolver? GCXURefs { get; set; }
        /// <summary>
        /// When the type of the custom data (<see cref="PICustomData"/>) is known, this contains
        ///  the type, as well as a function to get the custom data downcast to that type.
        /// </summary>
        public (Type type, Func<TExArgCtx, Expression> bpiAsType)? CustomDataType { get; set; }
        public Dictionary<string, Stack<Expression>> AliasStack { get; } =
            new();

        private static uint suffixNum = 0;
        public string NameWithSuffix(string s) => $"{s}CG{suffixNum++}";
       
#if EXBAKE_SAVE || EXBAKE_LOAD
        private static uint proxyArgNum = 0;
        public string NextProxyArg() => $"proxy{proxyArgNum}";
#endif
#if EXBAKE_SAVE
        public List<string> HoistedVariables { get; } = new List<string>();
        public Dictionary<Expression, Expression> HoistedReplacements { get; } =
            new Dictionary<Expression, Expression>();
        public List<Type> ProxyTypes { get; } = new List<Type>();
#elif EXBAKE_LOAD
        public List<object> ProxyArguments { get; } = new List<object>();
#endif

        /// <summary>
        /// Handle baking/loading an argument to an expression-reflected function that is not itself an expression
        ///  and cannot be trivially converted into a code representation.
        /// <br/>Returns the argument for chaining convenience.
        /// </summary>
        public T Proxy<T>(T replacee) {
#if EXBAKE_SAVE
            ProxyTypes.Add(typeof(T));
            HoistedReplacements[Ex.Constant(replacee)] = Ex.Variable(typeof(T), NextProxyArg());  
#elif EXBAKE_LOAD
            ProxyArguments.Add(replacee);
#endif
            return replacee;
        }
    }

    /// <inheritdoc cref="RootCtx.Proxy{T}"/>
    public T Proxy<T>(T replacee) => Ctx.Proxy(replacee);
    public readonly struct Arg {
        public readonly string name;
        //typeof(TExPI)
        public readonly Type texType;
        public readonly TEx expr;
        public readonly bool hasTypePriority;

        private Arg(string name, Type texType, TEx expr, bool hasTypePriority) {
            this.name = name;
            this.texType = texType;
            this.expr = expr;
            this.hasTypePriority = hasTypePriority;
        }

        public static Arg Make(string name, TEx expr, bool hasTypePriority) =>
            new(name, expr.GetType(), expr, hasTypePriority);

        //t = typeof(float) or similar
        public static Arg Make<T>(string name, bool hasTypePriority, bool isRef = false) {
            var expr = TEx.MakeParameter<T>(isRef, name);
            return Make(name, expr, hasTypePriority);
        }

        public static Arg MakeBPI => Arg.Make<ParametricInfo>("bpi", true);
    }
    
    public class LocalLet : IDisposable {
        private readonly string alias;
        private readonly TExArgCtx ctx;

        public LocalLet(TExArgCtx ctx, string alias, Expression val) {
            this.alias = alias;
            (this.ctx = ctx).Ctx.AliasStack.Push(alias, val);
        }

        public void Dispose() {
            ctx.Ctx.AliasStack.Pop(alias);
        }
    }

    public LocalLet Let(string alias, Expression val) => new(this, alias, val);
    
    private readonly Arg[] args;
    public IEnumerable<Expression> Expressions => args.Select(a => (Expression)a.expr);
    private readonly Dictionary<string, int> argNameToIndexMap;
    //Maps typeof(TExPI) to index
    private readonly Dictionary<Type, int> argExTypeToIndexMap;
    //Maps typeof(ParametricInfo) to index
    private readonly Dictionary<Type, int> argTypeToIndexMap;

    private readonly RootCtx? ctx;
    private readonly TExArgCtx? parent;
    public RootCtx Ctx => ctx ?? parent?.Ctx ?? throw new StaticException("No RootCtx found");
    private TExPI? _bpi;
    public TExPI BPI => _bpi ??= GetByExprType<TExPI>();
    public TExPI? MaybeBPI => _bpi ??= MaybeGetByExprType<TExPI>(out _);
    public Expression FCTX => BPI.FiringCtx;

    public UnaryExpression findex => BPI.findex;
    public MemberExpression id => BPI.id;
    public MemberExpression index => BPI.index;
    public MemberExpression LocV2 => BPI.locV2;
    public MemberExpression LocV3 => BPI.locV3;
    public MemberExpression locx => BPI.locx;
    public MemberExpression locy => BPI.locy;
    public MemberExpression locz => BPI.locz;
    public Expression t => BPI.t;
    public TEx<float> FloatVal => GetByExprType<TEx<float>>();
    public TExSB SB => GetByExprType<TExSB>();
    public TExGCX GCX => GetByExprType<TExGCX>();
    public TEx EnvFrame => GetByType<EnvFrame>();

    public TExArgCtx(params Arg[] args) : this(null, args) { }
    public TExArgCtx(TExArgCtx? parent, params Arg[] args) {
        this.parent = parent;
        if (parent == null) this.ctx = new RootCtx();
        this.args = args;
        argNameToIndexMap = new Dictionary<string, int>();
        argTypeToIndexMap = new Dictionary<Type, int>();
        argExTypeToIndexMap = new Dictionary<Type, int>();
        for (int ii = 0; ii < args.Length; ++ii) {
            if (argNameToIndexMap.ContainsKey(args[ii].name)) {
                throw new CompileException($"Duplicate argument name: {args[ii].name}");
            }
            argNameToIndexMap[args[ii].name] = ii;
            
            if (!argTypeToIndexMap.TryGetValue(args[ii].expr.type, out var i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argTypeToIndexMap[args[ii].expr.type] = ii;
            }
            if (!argExTypeToIndexMap.TryGetValue(args[ii].texType, out i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argExTypeToIndexMap[args[ii].texType] = ii;
            }
        }
    }

    public TEx<T> GetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new CompileException($"The variable \"{name}\" is not provided as an argument.");
        return args[idx].expr is TEx<T> arg ?
            arg :
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{args.Length}) is not of type {typeof(T).RName()}");
    }
    public TEx<T>? MaybeGetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return args[idx].expr is TEx<T> arg ?
            arg :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{args.Length}) is not of type {typeof(T).RName()}");
    }
    
    public TEx? MaybeGetByName(Type t, string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return args[idx].expr.GetType().GetGenericArguments()[0] == t ?
            args[idx].expr :
            //Still throw an error in this case
            throw new BadTypeException($"The variable \"{name}\" (#{idx+1}/{args.Length}) is not of type {t.RName()}");
    }
    
    public TEx GetByType<T>(out int idx) {
        if (!argTypeToIndexMap.TryGetValue(typeof(T), out idx))
            throw new CompileException($"No variable of type {typeof(T).RName()} is provided as an argument.");
        return args[idx].expr;
    }
    public TEx GetByType<T>() => GetByType<T>(out _);
    public TEx? MaybeGetByType<T>(out int idx) => 
        argTypeToIndexMap.TryGetValue(typeof(T), out idx) ? 
            args[idx].expr : 
            null;
    
    public Tx GetByExprType<Tx>(out int idx) where Tx : TEx {
        if (!argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx))
            throw new CompileException($"No variable of type {typeof(Tx).RName()} is provided as an argument.");
        return (Tx)args[idx].expr;
    }
    public Tx GetByExprType<Tx>() where Tx : TEx => GetByExprType<Tx>(out _);
    public Tx? MaybeGetByExprType<Tx>(out int idx) where Tx : TEx => 
        argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx) ? 
            (Tx) args[idx].expr : 
            null;
    
    public TExArgCtx Rehash() {
        var bpi = GetByExprType<TExPI>(out var bidx);
        return MakeCopyWith(bidx, Arg.Make(args[bidx].name, new TExPI(bpi.Rehash()), args[bidx].hasTypePriority));
    }
    public TExArgCtx CopyWithT(Expression newT) {
        var bpi = GetByExprType<TExPI>(out var bidx);
        return MakeCopyWith(bidx, Arg.Make(args[bidx].name, new TExPI(bpi.CopyWithT(newT)), args[bidx].hasTypePriority));
    }

    private TExArgCtx MakeCopyWith(int idx, Arg newArg) {
        var newargs = args.ToArray();
        newargs[idx] = newArg;
        return new TExArgCtx(this, newargs);
    }

    public TExArgCtx MakeCopyForType<T>(out TEx<T> currEx, out TEx<T> copyEx)  {
        currEx = (Expression)GetByType<T>(out int idx);
        copyEx = new TEx<T>();
        return MakeCopyWith(idx, Arg.Make(args[idx].name, copyEx, args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForType<T>(TEx<T> newEx) {
        _ = GetByType<T>(out int idx);
        return MakeCopyWith(idx, Arg.Make(args[idx].name, newEx, args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForExType<T>(out T currEx, out T copyEx) where T: TEx, new() {
        currEx = GetByExprType<T>(out int idx);
        copyEx = new T();
        return MakeCopyWith(idx, Arg.Make(args[idx].name, copyEx, args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForExType<T>(T newEx) where T: TEx {
        _ = GetByExprType<T>(out int idx);
        return MakeCopyWith(idx, Arg.Make(args[idx].name, newEx, args[idx].hasTypePriority));
    }

    public TExArgCtx Append(string name, TEx ex, bool hasPriority=true) {
        var newArgs = args.Append(Arg.Make(name, ex, hasPriority)).ToArray();
        return new TExArgCtx(this, newArgs);
    }
    public TExArgCtx AppendSB(string name, TExSB ex, bool hasPriority=true) {
        var nargs = args.Append(Arg.Make(name, ex, hasPriority));
        if (MaybeGetByExprType<TExPI>(out _) == null) nargs = nargs.Append(Arg.Make(name + "_bpi", ex.bpi, true));
        return new TExArgCtx(this, nargs.ToArray());
    }
    
    public Expression When(Func<TExArgCtx, TEx<bool>> pred, Expression then) => Expression.IfThen(pred(this), then);

    //Methods for dynamic (dict-based) data lookup
    public Expression DynamicHas<T>(string key) => PICustomData.ContainsDynamic<T>(this, key);
    public Expression DynamicGet<T>(string key) => PICustomData.GetValueDynamic<T>(this, key);
    public Expression DynamicSet<T>(string key, Expression val) => PICustomData.SetValueDynamic<T>(this, key, val);
}

/// <summary>
/// Base class for <see cref="TEx{T}"/> used for type constraints.
/// </summary>
public class TEx {
    protected readonly Expression ex;
    public readonly Type type;
    protected TEx(Expression ex) {
        this.ex = ex;
        this.type = ex.Type;
    }
    private static readonly IReadOnlyDictionary<Type, Type> TExBoxMap = new Dictionary<Type, Type> {
        { typeof(Vector2), typeof(TExV2) },
        { typeof(Vector3), typeof(TExV3) },
        { typeof(ParametricInfo), typeof(TExPI) },
        { typeof(float), typeof(TEx<float>) },
        { typeof(V2RV2), typeof(TExRV2) },
    };
    private static readonly Type TypeTExT = typeof(TEx<>);
    
    public static TEx Box(Expression ex) {
        var ext = ex.Type;
        if (!TExBoxMap.TryGetValue(ext, out var tt)) throw new Exception($"Cannot box expression of type {ext}");
        return Activator.CreateInstance(tt, ex) as TEx ?? throw new Exception("Boxing failed");
    }

    //t = typeof(float) or similr
    public static TEx MakeParameter<T>(bool isRef, string name) {
        var t = typeof(T);
        var rt = (isRef) ? t.MakeByRefType() : t;
        var ex = Expression.Parameter(rt, name);
        if (t == tv2)
            return new TExV2(ex);
        else if (t == tv3)
            return new TExV3(ex);
        else if (t == typeof(ParametricInfo))
            return new TExPI(ex);
        else if (t == typeof(BulletManager.SimpleBullet))
            return new TExSB(ex);
        else if (t == typeof(V2RV2))
            return new TExRV2(ex);
        else if (t == typeof(BulletManager.SimpleBulletCollection))
            return new TExSBC(ex);
        else if (t == typeof(BulletManager.SimpleBulletCollection.VelocityUpdateState))
            return new TExSBCUpdater(ex);
        else if (t == typeof(Movement))
            return new TExMov(ex);
        else if (t == typeof(LaserMovement))
            return new TExLMov(ex);
        else if (t == typeof(GenCtx))
            return new TExGCX(ex);
        else
            return new TEx<T>(ex);
    }

    protected TEx(ExMode mode, Type t, string? name) {
        if (mode == ExMode.RefParameter) {
            t = t.MakeByRefType();
        }
        ex = name == null ? Expression.Parameter(t) : Expression.Parameter(t, name);
        this.type = ex.Type;
    }
    public static implicit operator TEx(Expression ex) {
        return new(ex);
    }
    public static implicit operator Expression(TEx me) {
        return me.ex;
    }
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }
    
    private static Ex ResolveCopy(Func<Ex[], Ex> func, params (Ex ex, bool reqCopy)[] args) {
        var newvars = ListCache<ParameterExpression>.Get();
        var setters = ListCache<Expression>.Get();
        var usevars = new Expression[args.Length];
        for (int ii = 0; ii < args.Length; ++ii) {
            var (ex, reqCopy) = args[ii];
            if (reqCopy) {
                //Don't name this, as nested TEx should not overlap
                var copy = V(ex.Type);
                usevars[ii] = copy;
                newvars.Add(copy);
                setters.Add(copy.Is(ex));
            } else {
                usevars[ii] = ex;
            }
        }
        setters.Add(func(usevars));
        var block = Ex.Block(newvars, setters);
        ListCache<ParameterExpression>.Consign(newvars);
        ListCache<Expression>.Consign(setters);
        return setters.Count > 1 ? func(usevars) : block;
    }
    public static Ex ResolveF(TEx<float> t1, Func<TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex Resolve<T1>(TEx<T1> t1, Func<TEx<T1>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex ResolveV2(TEx<Vector2> t1, Func<TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0])), t1);
    public static Ex ResolveV3(TEx<Vector3> t1, Func<TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0])), t1);
    public static Ex Resolve<T1,T2>(TEx<T1> t1, TEx<T2> t2, Func<TEx<T1>, TEx<T2>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1]), t1, t2);
    public static Ex ResolveV2(TEx<Vector2> t1, TEx<Vector2> t2, 
        Func<TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1])), t1, t2);
    public static Ex ResolveV2(TEx<Vector2> t1, TEx<float> t2, 
        Func<TExV2, TEx<float>, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), x[1]), t1, t2);
    /// <inheritdoc cref="Resolve{T1,T2,T3}"/>
    public static Ex ResolveV3(TEx<Vector3> t1, TEx<Vector3> t2, 
        Func<TExV3, TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0]), new TExV3(x[1])), t1, t2);
    /// <summary>
    /// Copy the provided expressions into temporary variables that can be reused without recalculating the expression.
    /// </summary>
    public static Ex Resolve<T1,T2,T3>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2]), t1, t2, t3);
    public static Ex ResolveV2(TEx<Vector2> t1, TEx<Vector2> t2, TEx<Vector2> t3, 
        Func<TExV2, TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1]), new TExV2(x[2])), t1, t2, t3);
    public static Ex Resolve<T1,T2,T3,T4>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3]), t1, t2, t3, t4);
    public static Ex Resolve<T1,T2,T3,T4,T5>(TEx<T1> t1, TEx<T2> t2, TEx<T3> t3, TEx<T4> t4, TEx<T5> t5, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4]), t1, t2, t3, t4, t5);
    
    public static bool RequiresCopyOnRepeat(Expression e) => !(
        e.NodeType == ExpressionType.Parameter ||
        e.NodeType == ExpressionType.Constant ||
        e.NodeType == ExpressionType.MemberAccess ||
        (e.NodeType == ExpressionType.Convert && !RequiresCopyOnRepeat((e as UnaryExpression)!.Operand)));
    
    
    public static implicit operator (Ex, bool)(TEx exx) => (exx.ex, RequiresCopyOnRepeat(exx.ex));
}
/// <summary>
/// A typed expression.
/// <br/>This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// <br/>However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression eg(float).</typeparam>
public class TEx<T> : TEx {

    public TEx() : this(ExMode.Parameter, null) {}

    public TEx(Expression ex) : base(ex) { }

    public TEx(ExMode m, string? name) : base(m, typeof(T), name) {}
    
    public static implicit operator TEx<T>(Expression ex) {
        return new(ex);
    }

    public static implicit operator TEx<T>(T obj) => Expression.Constant(obj);
}
}