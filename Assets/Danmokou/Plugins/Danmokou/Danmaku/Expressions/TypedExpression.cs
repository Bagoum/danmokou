using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using static Danmokou.Expressions.ExUtils;

namespace Danmokou.Expressions {

/// <summary>
/// Experimental context storing an arbitrary set of arguments to an expression function.
/// </summary>
public class TExArgCtx {
    /// <summary>
    /// Context that is shared by any copies of this.
    /// </summary>
    public class RootCtx {
        public ReflectEx.ICompileReferenceResolver? ICRR { get; set; }
        public Dictionary<string, Stack<Expression>> AliasStack { get; } =
            new Dictionary<string, Stack<Expression>>();

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
    }
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
            new Arg(name, expr.GetType(), expr, hasTypePriority);

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

    public LocalLet Let(string alias, Expression val) => new LocalLet(this, alias, val);
    
    private readonly Arg[] args;
    public IEnumerable<Expression> Expressions => args.Select(a => (Expression)a.expr);
    private readonly Dictionary<string, int> argNameToIndexMap;
    //Maps typeof(TExPI) to index
    private readonly Dictionary<Type, int> argExTypeToIndexMap;
    //Maps typeof(ParametricInfo) to index
    //private readonly Dictionary<Type, int> argTypeToIndexMap;

    private readonly RootCtx? ctx;
    private readonly TExArgCtx? parent;
    public RootCtx Ctx => ctx ?? parent?.Ctx ?? throw new Reflector.StaticException("No RootCtx found");
    private TExPI? _bpi;
    public TExPI BPI => _bpi ??= GetByExprType<TExPI>();
    public TExPI? MaybeBPI => _bpi ??= MaybeGetByExprType<TExPI>(out _);
    public Expression FCTX => BPI.FiringCtx;

    public UnaryExpression findex => BPI.findex;
    public MemberExpression id => BPI.id;
    public MemberExpression index => BPI.index;
    public MemberExpression loc => BPI.loc;
    public MemberExpression locx => BPI.locx;
    public MemberExpression locy => BPI.locy;
    public Expression t => BPI.t;
    public TEx<float> FloatVal => GetByExprType<TEx<float>>();
    public TExSB SB => GetByExprType<TExSB>();
    public TExGCX GCX => GetByExprType<TExGCX>();

    public TExArgCtx(params Arg[] args) : this(null, args) { }
    public TExArgCtx(TExArgCtx? parent, params Arg[] args) {
        this.parent = parent;
        if (parent == null) this.ctx = new RootCtx();
        this.args = args;
        argNameToIndexMap = new Dictionary<string, int>();
        //argTypeToIndexMap = new Dictionary<Type, int>();
        argExTypeToIndexMap = new Dictionary<Type, int>();
        for (int ii = 0; ii < args.Length; ++ii) {
            if (argNameToIndexMap.ContainsKey(args[ii].name)) {
                throw new Reflector.CompileException($"Duplicate argument name: {args[ii].name}");
            }
            argNameToIndexMap[args[ii].name] = ii;
            /*
            if (!argTypeToIndexMap.TryGetValue(args[ii].expr.type, out var i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argTypeToIndexMap[args[ii].expr.type] = ii;
            }*/
            if (!argExTypeToIndexMap.TryGetValue(args[ii].texType, out var i)
                || !args[i].hasTypePriority
                || args[ii].hasTypePriority) {
                argExTypeToIndexMap[args[ii].texType] = ii;
            }
        }
    }

    public static TExArgCtx FromBPI(TExPI bpi, string name) => new TExArgCtx(Arg.Make(name, bpi, true));

    public TEx<T> GetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            throw new Reflector.CompileException($"The variable \"{name}\" is not provided as an argument.");
        return args[idx].expr is TEx<T> arg ?
            arg :
            throw new Reflector.BadTypeException($"The variable \"{name}\" (#{idx+1}/{args.Length}) is not of type {typeof(T).RName()}");
    }
    public TEx<T>? MaybeGetByName<T>(string name) {
        if (!argNameToIndexMap.TryGetValue(name, out var idx))
            return null;
        return args[idx].expr is TEx<T> arg ?
            arg :
            //Still throw an error in this case
            throw new Reflector.BadTypeException($"The variable \"{name}\" (#{idx+1}/{args.Length}) is not of type {typeof(T).RName()}");
    }
    public Tx GetByExprType<Tx>(out int idx) where Tx : TEx {
        if (!argExTypeToIndexMap.TryGetValue(typeof(Tx), out idx))
            throw new Reflector.CompileException($"No variable of type {typeof(Tx).RName()} is provided as an argument.");
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

    public TExArgCtx MakeCopyForType<T>(out T currEx, out T copyEx) where T : TEx, new() {
        currEx = GetByExprType<T>(out int idx);
        copyEx = new T();
        return MakeCopyWith(idx, Arg.Make(args[idx].name, copyEx, args[idx].hasTypePriority));
    }
    
    public TExArgCtx MakeCopyForType<T>(T newEx) where T: TEx {
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

    public Expression FCtxHas<T>(string key) => FiringCtx.Contains<T>(this, key);
    public Expression FCtxGet<T>(string key) => FiringCtx.GetValue<T>(this, key);
    public Expression FCtxSet<T>(string key, Expression val) => FiringCtx.SetValue<T>(this, key, val);
}

/// <summary>
/// Base class for TEx{T} used for type constraints.
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
        else if (t == typeof(BulletManager.AbsSimpleBulletCollection))
            return new TExSBC(ex);
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
        return new TEx(ex);
    }
    public static implicit operator Expression(TEx me) {
        return me.ex;
    }
    public static implicit operator ParameterExpression(TEx me) {
        return (ParameterExpression)me.ex;
    }
}
/// <summary>
/// A typed expression.
/// This typing is syntactic sugar: any expression, regardless of type, can be cast as eg. TEx{float}.
/// However, constructing a parameter expression via TEx{T} will type the expression appropriately.
/// By default, creates a ParameterExpression.
/// </summary>
/// <typeparam name="T">Type of expression.</typeparam>
public class TEx<T> : TEx {

    public TEx() : this(ExMode.Parameter, null) {}

    public TEx(Expression ex) : base(ex) { }

    public TEx(ExMode m, string? name) : base(m, typeof(T), name) {}
    
    public static implicit operator TEx<T>(Expression ex) {
        return new TEx<T>(ex);
    }

    public static implicit operator TEx<T>(T obj) => Expression.Constant(obj);

    public Expression GetExprDontUseThisGenerally() {
        return ex;
    }
}
}