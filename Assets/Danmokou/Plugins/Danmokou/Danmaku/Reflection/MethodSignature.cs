using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection2;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using MethodCall = Danmokou.Reflection2.AST.MethodCall;

namespace Danmokou.Reflection {


    //this doesn't implement IMethodDesignation because we don't want it to report TypeDesignation at this level,
    // as that would result in all invocations of the same method sharing the same type variables
    //instead, TypeDesignation is copied at the InvokedMethod level, preventing cross-contamination
    public interface IMethodSignature {
        /// <summary>
        /// Get a representation of this method's type. This should not directly be used for unification, as
        ///  its variable types should not be shared between all invocations.
        ///  Call <see cref="TypeDesignation.RecreateVariables"/> before using for unification.
        /// <br/>Note that lifted methods do NOT return a lifted type here. The types here are *unlifted*
        ///  over the TExArgCtx->TEx&lt;&gt; functor.
        /// <br/>Note that instance methods should prepend the instance type at the beginning of the argument array.
        /// </summary>
        TypeDesignation.Dummy SharedType { get; }
        
        /// <summary>
        /// The parameters of the method signature.
        /// </summary>
        Reflector.NamedParam[] Params { get; }
        
        /// <summary>Executable information for the method/constructor/field/property.</summary>
        TypeMember Member { get; }

        /// <summary>
        /// True if this is a fallthrough method (BDSL1 only).
        /// </summary>
        bool IsFallthrough => false;

        /// <summary>
        /// True if this method is a constructor.
        /// </summary>
        bool IsCtor => false;
        
        /// <summary>
        /// True if this is a static method.
        /// </summary>
        bool IsStatic { get; }
        
        /// <summary>
        /// The return type of this method.
        /// <br/>Lifted methods return a lifted return type.
        /// </summary>
        Type ReturnType { get; }
        
        /// <summary>
        /// The type declaring this method.
        /// </summary>
        Type? DeclaringType { get; }
        
        /// <summary>
        /// Show the signature of this method.
        /// </summary>
        string AsSignature { get; }
        
        /// <summary>
        /// Show the signature of this method, including type restrictions.
        /// </summary>
        string AsSignatureWithRestrictions { get; }

        [PublicAPI]
        public string AsSignatureWithParamMod(Func<Reflector.NamedParam, int, string> paramMod);
        
        /// <summary>
        /// Show the signature of this method, only including types and not names.
        /// </summary>
        string TypeOnlySignature { get; }

        Reflector.InvokedMethod Call(string? calledAs);

        /// <summary>
        /// Get an attribute defined on the method.
        /// </summary>
        T? GetAttribute<T>() where T : Attribute;

        /// <summary>
        /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
        /// </summary>
        object? Invoke(MethodCall? ast, object?[] args);
        
        /// <summary>
        /// Invoke this method. If this is an instance method, the instance should be the first argument of `args`.
        /// </summary>
        Expression InvokeEx(MethodCall? ast, params Expression[] args);
        
        /// <summary>
        /// Return the invocation of this method as an expression node,
        /// but if all arguments are constant, then instead call the method and wrap it in Ex.Constant.
        /// </summary>
        public Expression InvokeExIfNotConstant(MethodCall? ast, params Expression[] args) {
            for (int ii = 0; ii < args.Length; ++ii)
                if (args[ii] is not ConstantExpression)
                    return InvokeEx(ast, args);
            return Expression.Constant(Invoke(ast, args.Select(a => ((ConstantExpression)a).Value).ToArray()));
        }

        /// <summary>
        /// If this method is defined in a file, make a link to the file.
        /// </summary>
        string? MakeFileLink(string typName);

        /// <summary>
        /// (Informational) The name of the type declaring this method.
        /// </summary>
        string TypeName { get; }
        
        /// <summary>
        /// (Informational) The name of this method.
        /// </summary>
        /// <returns></returns>
        string Name { get; }
    }



    /// <summary>
    /// An annotated method signature. This may be for a static/instance function, constructor, field, or property.
    /// </summary>
    public record MethodSignature : IMethodSignature {
        private static readonly Dictionary<MemberInfo, MethodSignature> globals = new();
        public TypeMember Member { get; init; }
        /// <summary>
        /// Simplified description of the method parameters. Lifted methods have lifted parameters.
        /// </summary>
        public Reflector.NamedParam[] Params { get; init; }
        /// <inheritdoc/>
        public TypeDesignation.Dummy SharedType { get; init; }
        public Dictionary<Type, TypeDesignation.Variable> GenericTypeMap { get; private set; }
        /// <inheritdoc cref="IGenericMethodSignature.SharedGenericTypes"/>
        public TypeDesignation.Variable[] SharedGenericTypes { get; init; } = Array.Empty<TypeDesignation.Variable>();

        protected MethodSignature(TypeMember Member, Reflector.NamedParam[] Params) {
            this.Member = Member;
            this.Params = Params;
            GenericTypeMap = new Dictionary<Type, TypeDesignation.Variable>();
            if (Member.BaseMi is MethodBase { IsGenericMethodDefinition: true } mi) {
                var typeRestrs = mi.GetCustomAttributes<RestrictTypesAttribute>()
                    .ToDictionary(a => a.typeIndex, 
                        a => a.possibleTypes.Select(t => new TypeDesignation.Known(t)).ToArray());
                SharedGenericTypes = mi.GetGenericArguments()
                    .Select((t, i) => GenericTypeMap[t] = new TypeDesignation.Variable()
                        //TryGetValueOrDefault doesn't work on language server
                        { RestrictedTypes = typeRestrs.TryGetValue(i, out var v) ? v : null })
                    .ToArray();
            }
            TypeDesignation DesignationForWrappedType(Type t) {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<,>)) {
                    var gargs = t.GetGenericArguments();
                    if (gargs[0] == typeof(TExArgCtx))
                        return DesignationForWrappedType(gargs[1]);
                } else if (t == typeof(TEx))
                    //"void-typed" tex- use a variable
                    return new TypeDesignation.Variable();
                else if (t.IsTExType(out var inner))
                    //typed tex- use the type
                    return TypeDesignation.FromType(inner, GenericTypeMap);
                return TypeDesignation.FromType(t, GenericTypeMap);
            }
            
            SharedType = TypeDesignation.Dummy.Method(DesignationForWrappedType(ReturnType), 
                    Params.Select(p => DesignationForWrappedType(p.Type)).ToArray());
        }
        public bool IsFallthrough { get; init; } = false;
        public string TypeName => Member.TypeName;
        public string Name => Member.Name;
        public bool IsCtor => Member.Name == ".ctor";
        public bool IsStatic => Member.Static;
        public Type? DeclaringType => Member.BaseMi.DeclaringType;

        public virtual Type ReturnType => Member.ReturnType;

        public string TypeOnlySignature => Member.TypeOnlySignature();

        public string AsSignature => AsSignatureWithParamMod((p, _) => p.AsParameter);

        public string AsSignatureWithRestrictions {
            get {
                var sig = AsSignature;
                var restr = GenericTypeMap.Where(kv => kv.Value.RestrictedTypes != null).ToList();
                if (restr.Count == 0) return sig;
                var restrStrs = restr.Select(kv =>
                    $"{kv.Key.RName()}:{string.Join(",", kv.Value.RestrictedTypes!.Select(t => t.SimpRName()))}");
                return $"{sig} where {string.Join("; ", restrStrs)}";
            }
        }

        public string AsSignatureWithParamMod(Func<Reflector.NamedParam, int, string> paramMod) =>
            Member.AsSignature(paramMod);

        /// <summary>
        /// Number of parameters that must be parsed by reflection.
        /// </summary>
        public int ExplicitParameterCount(int start = 0, int? end = null) {
            var ct = 0;
            for (int ii = start; ii < (end ?? Params.Length); ++ii)
                if (!Params[ii].NonExplicit)
                    ++ct;
            return ct;
        }

        public T? GetAttribute<T>() where T : Attribute => Member.GetAttribute<T>();

        /// <summary>
        /// Invoke the method with the provided arguments.
        /// </summary>
        public virtual object? Invoke(MethodCall? ast, params object?[] args) =>
            Member.Invoke(args);
        
        /// <summary>
        /// Return the invocation of this method as an expression node.
        /// </summary>
        public virtual Ex InvokeEx(MethodCall? ast, params Ex[] args) =>
            Member.InvokeEx(args);

        public string? MakeFileLink(string typName) =>
            Member.BaseMi.DeclaringType!.GetCustomAttribute<ReflectAttribute>(false)?.FileLink(typName);

        public virtual Reflector.InvokedMethod Call(string? calledAs) => new(this, calledAs);

        /// <summary>
        /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
        /// </summary>
        public static MethodSignature Get(MemberInfo mi) => 
            MaybeGet(mi) ?? throw new Exception($"Member {mi} cannot be handled by reflection.");
        
        /// <summary>
        /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
        /// </summary>
        public static MethodSignature? MaybeGet(MemberInfo mi) {
            if (globals.TryGetValue(mi, out var sig))
                return sig;
            var member = TypeMember.MaybeMake(mi);
            if (member == null)
                return null;
            return Get(member);
        }

        public static MethodSignature Get(TypeMember member) {
            var mi = member.BaseMi;
            var fallthrough = mi.GetCustomAttribute<FallthroughAttribute>() != null;
            if (member is TypeMember.Method { Mi : {IsGenericMethodDefinition : true} } inf)
                return globals[mi] = new GenericMethodSignature(inf, member.Params) { IsFallthrough = fallthrough };
            return globals[mi] = new(member, member.Params) { IsFallthrough = fallthrough };
        }

        public ScopedConversionKind ImplicitTypeConvKind =>
            GetAttribute<ExpressionBoundaryAttribute>() != null ?
                ScopedConversionKind.BlockScopedExpression :
                ScopedConversionKind.Trivial;
    

        public virtual LiftedMethodSignature<T> Lift<T>() => LiftedMethodSignature<T>.Lift(this);

        private object? _asFunc;
        public object AsFunc() {
            if (_asFunc != null) return _asFunc;
            if (SharedGenericTypes.Length > 0)
                throw new Exception("Cannot convert a generic method to a partial function");
            var fTypes = SharedType.Arguments.Select(t => t.Resolve().LeftOrThrow).ToArray();
            var args = new IDelegateArg[Params.Length];
            for (int ii = 0; ii < Params.Length; ++ii)
                args[ii] = new DelegateArg($"$parg{ii}", fTypes[ii]);
            var fnType = ReflectionUtils.MakeFuncType(fTypes);
            Func<TExArgCtx, TEx> body = tac => MethodCall.RealizeMethod(null, this, tac, 
                (i, tac) => tac.GetByName(fTypes[i], $"$parg{i}"));
            return _asFunc = CompilerHelpers.CompileDelegateMeth.Specialize(fnType).Invoke(null, body, args)!;
        }
    }
    

    public interface IGenericMethodSignature : IMethodSignature {
        /// <summary>
        /// Make a concrete method out of a generic one using the provided type parameter.
        /// </summary>
        MethodSignature Specialize(params Type[] t);
        
        /// <summary>
        /// Get the type designations for each of the generic types of this method.
        /// Note this should not be used for unification, as it is shared between all invocations.
        /// </summary>
        TypeDesignation.Variable[] SharedGenericTypes { get; }
    }

    /// <inheritdoc cref="MethodSignature"/>
    public record GenericMethodSignature(TypeMember.Method Minf, Reflector.NamedParam[] Params) : MethodSignature(Minf, Params), IGenericMethodSignature {
        public int TypeParams { get; } = Minf.Mi.GetGenericArguments().Length;
        public static readonly Dictionary<(FreezableArray<Type>, MethodSignature), MethodSignature> specializeCache = new();
        
        public override object Invoke(MethodCall? ast, params object?[] prms) {
            throw new Exception("A generic method signature cannot be invoked");
        }

        public override Ex InvokeEx(MethodCall? ast, params Ex[] args) {
            throw new Exception("A generic method signature cannot be invoked");
        }

        public MethodSignature Specialize(params Type[] t) {
            var typDef = new FreezableArray<Type>(t);
            var specialized = specializeCache.TryGetValue((typDef, this), out var m) ?
                m :
                specializeCache[(typDef, this)] = MethodSignature.Get(Minf.Mi.MakeGenericMethod(t));
            return specialized;
        }

        public override LiftedMethodSignature<T> Lift<T>() => LiftGeneric<T>();

        public GenericLiftedMethodSignature<T> LiftGeneric<T>() => 
            LiftedMethodSignature<T>.Lift(this) as GenericLiftedMethodSignature<T> ??
            throw new StaticException("Incorrect lifting behavior on generic method signature");
    }

    /// <summary>
    /// A description of a funcified method called in reflection.
    /// <br/>A funcified method has a "source" signature (A, B, C)->R, but is internally
    /// converted to "funcified" signature (T->A, T->B, T->C)->(T->R);
    ///  ie. it is lifted over the reader functor. This is because
    /// some internal reflection functions are of type <see cref="TExArgCtx"/>->TEx,
    ///  but it is generally easier to write them as type TEx where possible.
    /// </summary>
    /// <param name="Original">The source method, with the signature (A, B, C)->R.</param>
    /// <param name="FuncedParams">The parameter list [T->A, T->B, T->C]. This is provided as <see cref="MethodSignature.Params"/>.</param>
    /// <param name="BaseParams">The parameter list [A, B, C].</param>
    public abstract record LiftedMethodSignature(MethodSignature Original, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) 
        : MethodSignature(Original.Member, FuncedParams) {
        protected static readonly Dictionary<(Type, Type), (Type lmsTR, ConstructorInfo constr)> typeSpecCache = new();
        protected static readonly Type[] consTypes = { typeof(MethodSignature), typeof(Reflector.NamedParam[]), typeof(Reflector.NamedParam[]) };

        public override Reflector.InvokedMethod Call(string? calledAs) => new Reflector.LiftedInvokedMethod(this, calledAs);
        
        public override object? Invoke(MethodCall? ast, params object?[] prms) {
            throw new Exception(
                "This lifted method signature does not have a specified return type and therefore cannot be invoked");
        }

        public override Ex InvokeEx(MethodCall? ast, params Ex[] args) {
            throw new Exception("Lifted methods cannot be invoked as expressions");
        }

        /// <summary>
        /// Lift a set of parameters over the reader functor T->.
        /// </summary>
        public static Reflector.NamedParam[] LiftParams<T>(MethodSignature method) => LiftParams(typeof(T), method);
        
        public static Reflector.NamedParam[] LiftParams(Type t, MethodSignature method) {
            var baseTypes = method.Params;
            var fTypes = new Reflector.NamedParam[baseTypes.Length];
            for (int ii = 0; ii < baseTypes.Length; ++ii) {
                var bt = baseTypes[ii].Type;
                fTypes[ii] = baseTypes[ii] with {
                    Type = Reflector.ReflectionData.LiftType(t, bt, out var result) ? result : bt
                };
            }
            return fTypes;
        }
    }

    /// <inheritdoc cref="LiftedMethodSignature"/>
    public abstract record LiftedMethodSignature<T>(MethodSignature Original, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) :
        LiftedMethodSignature(Original, FuncedParams, BaseParams) {
        private static readonly Dictionary<MemberInfo, LiftedMethodSignature<T>> liftCache = new();
        
        /// <summary>
        /// Lift a method over the reader functor T->.
        /// <br/>If R is known statically, use <see cref="LiftedMethodSignature{T,R}"/>'s Lift instead.
        /// </summary>
        public static LiftedMethodSignature<T> Lift(MethodSignature method) {
            if (liftCache.TryGetValue(method.Member.BaseMi, out var sig))
                return sig;
            if (method is LiftedMethodSignature)
                throw new Exception("Tried to lift a method twice");
            if (method is GenericMethodSignature gm)
                return liftCache[method.Member.BaseMi] = new GenericLiftedMethodSignature<T>(gm, gm.Minf, LiftParams<T>(gm), gm.Params);
            return MakeForReturnType(method.ReturnType, method);
        }

        private static LiftedMethodSignature<T> MakeForReturnType(Type r, MethodSignature method) {
            var t = typeof(T);
            if (!typeSpecCache.TryGetValue((t, r), out var info)) {
                var type = typeof(LiftedMethodSignature<,>).MakeGenericType(t, r);
                var cons = type.GetConstructor(consTypes);
                typeSpecCache[(t, r)] = info = (type, cons);
            }
            return liftCache[method.Member.BaseMi] = info.constr!.Invoke(new object[] { method, LiftParams(t, method), method.Params })
                as LiftedMethodSignature<T> ?? throw new StaticException(
                $"Dynamic instantiation of LiftedMethodSignature<{t.SimpRName()},{r.SimpRName()}> failed");
        }
    }
    
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record GenericLiftedMethodSignature<T>(MethodSignature Original, TypeMember.Method Minf, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams), IGenericMethodSignature  {
        public override Type ReturnType => Reflector.Func2Type(typeof(T), base.ReturnType);

        public override object Invoke(MethodCall? ast, params object?[] prms)
            => throw new Exception("A generic lifted method cannot be invoked");
        
        public override Ex InvokeEx(MethodCall? ast, params Ex[] args) {
            throw new Exception("Lifted methods cannot be invoked as expressions");
        }
        
        public LiftedMethodSignature<T> Specialize(Type[] t) {
            var typDef = new FreezableArray<Type>(t);
            LiftedMethodSignature<T> method;
            if (GenericMethodSignature.specializeCache.TryGetValue((typDef, this), out var m))
                method = m as LiftedMethodSignature<T> ??
                       throw new StaticException("Cached specialization of lifted generic method failed");
            else {
                GenericMethodSignature.specializeCache[(typDef, this)] = method = 
                    MethodSignature.Get(Minf.Mi.MakeGenericMethod(t)).Lift<T>();
            }
            return method;
        }

        MethodSignature IGenericMethodSignature.Specialize(Type[] t) => Specialize(t);
    }
    
    //Note that we must eventually specify the R in LiftedMethodSignature in order to ensure that
    // InvokeMiFunced creates a correctly-typed Func<T,R>.
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record LiftedMethodSignature<T, R>(MethodSignature Original, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) 
        : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams) {
        private static readonly Dictionary<MemberInfo, LiftedMethodSignature<T, R>> liftCache = new();
        public override Type ReturnType => typeof(Func<T, R>);

        public override object Invoke(MethodCall? ast, params object?[] prms) {
            return InvokeMiFunced(ast, prms);
        }
        
        public override Ex InvokeEx(MethodCall? ast, params Ex[] args) {
            throw new Exception("Lifted methods cannot be invoked as expressions");
        }

        public Func<T,R> InvokeMiFunced(MethodCall? ast, object?[] fprms) => 
            //Note: this lambda capture generally prevents using ArrayCache
            bpi => {
                var baseArgs = new object?[BaseParams.Length];
                for (int ii = 0; ii < baseArgs.Length; ++ii)
                    //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                    baseArgs[ii] = Reflector.ReflectionData.Defuncify(
                        BaseParams[ii].Type, FuncedParams[ii].Type, fprms[ii]!, bpi!);
                
                return (R)Member.Invoke(baseArgs)!;
            };

        public override Reflector.InvokedMethod Call(string? calledAs) => new Reflector.LiftedInvokedMethod<T,R>(this, calledAs);

        /// <summary>
        /// Lift a method over the reader functor T->.
        /// <br/>If R is not known statically, use <see cref="LiftedMethodSignature{T}"/>'s Lift instead.
        /// </summary>
        public new static LiftedMethodSignature<T, R> Lift(MethodSignature method) {
            if (liftCache.TryGetValue(method.Member.BaseMi, out var sig))
                return sig;
            //funced methods are not fallthrough
            return liftCache[method.Member.BaseMi] = new(method, LiftedMethodSignature.LiftParams<T>(method), method.Params);
        }
    }

}