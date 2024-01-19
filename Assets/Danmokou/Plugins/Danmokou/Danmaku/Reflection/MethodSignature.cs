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
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection {
    /// <summary>
    /// An annotated method signature. This may be for a static/instance function, constructor, field, or property.
    /// </summary>
    public record MethodSignature : Reflector.IMethodSignature {
        private static readonly Dictionary<MemberInfo, MethodSignature> globals = new();
        /// <summary>Executable information for the method/constructor/field/property.</summary>
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

        /// <summary>
        /// If this is a partially applied method, then this contains the number of arguments provided.
        /// </summary>
        public int? PartialArgs { get; protected init; } = null;

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
            SharedType =
                TypeDesignation.FromMethod(ReturnType.MaybeUnwrapTExFuncType(), 
                    Params.Select(p => p.Type.MaybeUnwrapTExFuncType()), GenericTypeMap);
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
        /// Make a function of N arguments take only `args` arguments, by storing the remaining arguments in a lambda.
        /// <br/>eg. for a function (a,b,c,d,e)->f, calling PartiallyApply(2) would return a function
        /// of form (a,b)->Func&lt;c,d,e,f&gt;.
        /// </summary>
        public virtual MethodSignature PartiallyApply(int args) => this with {
                PartialArgs = args,
                SharedType = SharedType.PartialApplyToFunc(args)
            };

        /// <summary>
        /// Invoke the method with the provided arguments.
        /// </summary>
        public virtual object? Invoke(Reflection2.AST.MethodCall? ast, params object?[] args) =>
            PartialArgs is { } pargs ?
                LambdaHelpers.MakePartialFunc(Params.Length - pargs, ast, this, PartiallyAppliedFnTypeArgs(pargs, Params, ReturnType), args) :
                Member.Invoke(args);
        
        /// <summary>
        /// Return the invocation of this method as an expression node.
        /// </summary>
        public virtual Ex InvokeEx(Reflection2.AST.MethodCall? ast, params Ex[] args) =>
            PartialArgs is { } pargs ?
                throw new NotImplementedException("invoke ex partial") :
                Member.InvokeEx(args);

        public string? MakeFileLink(string typName) =>
            Member.BaseMi.DeclaringType!.GetCustomAttribute<ReflectAttribute>(false)?.FileLink(typName);

        public virtual Reflector.InvokedMethod Call(string? calledAs) => new(this, calledAs);

        /// <summary>
        /// Returns a <see cref="MethodSignature"/> or <see cref="GenericMethodSignature"/> for this method.
        /// </summary>
        public static MethodSignature Get(MemberInfo mi) {
            if (globals.TryGetValue(mi, out var sig))
                return sig;
            var member = TypeMember.Make(mi);
            var fallthrough = mi.GetCustomAttribute<FallthroughAttribute>() != null;
            if (member is TypeMember.Method { Mi : {IsGenericMethodDefinition : true} } inf)
                return globals[mi] = new GenericMethodSignature(inf, member.Params) { IsFallthrough = fallthrough };
            return globals[mi] = new(member, member.Params) { IsFallthrough = fallthrough };
        }

        public ScopedConversionKind ImplicitTypeConvKind =>
            GetAttribute<ExpressionBoundaryAttribute>() != null ?
                ScopedConversionKind.ScopedExpression :
                ScopedConversionKind.Trivial;
    

        public virtual LiftedMethodSignature<T> Lift<T>() => LiftedMethodSignature<T>.Lift(this);

        protected static Type[] PartiallyAppliedFnTypeArgs(int applied, Reflector.NamedParam[] prms, Type retType) =>
            prms.TakeLast(prms.Length - applied).Select(p => p.Type).Append(retType).ToArray();
        
        public static class LambdaHelpers {
            public static Func<R> PartialMissing0<R>(Reflection2.AST.MethodCall? ast, MethodSignature mi, object?[] prms) =>
                () => (R)mi.Invoke(ast, prms)!;
    
            public static Func<T1,R> PartialMissing1<T1,R>(Reflection2.AST.MethodCall? ast, MethodSignature mi, object?[] prms) =>
                t1 => (R)mi.Invoke(ast, prms.Append(t1).ToArray())!;
    
            public static Func<T1,T2,R> PartialMissing2<T1,T2,R>(Reflection2.AST.MethodCall? ast, MethodSignature mi, object?[] prms) =>
                (t1, t2) => (R)mi.Invoke(ast, prms.Concat(new object?[]{t1, t2}).ToArray())!;
    
            public static Func<T1,T2,T3,R> PartialMissing3<T1,T2,T3,R>(Reflection2.AST.MethodCall? ast, MethodSignature mi, object?[] prms) =>
                (t1, t2, t3) => (R)mi.Invoke(ast, prms.Concat(new object?[]{t1, t2, t3}).ToArray())!;

            private static readonly MethodInfo?[] methods = new MethodInfo[4];
 
            /// <summary>
            /// Create a Func representing the partial application of `prms` to `mi`.
            /// </summary>
            /// <example>
            /// mi = (A a,B b,C c,D d,E e)->X
            /// <br/>prms = [a,b,c]
            /// <br/>missing = 2
            /// <br/>fnTypeArgs = [typeof(D), typeof(E), typeof(X)]
            /// <br/>returns (Func&lt;D,E,X&gt;)((D d,E e)->mi.Invoke(a,b,c,d,e))
            /// </example>
            public static object MakePartialFunc(int missing, Reflection2.AST.MethodCall? ast, MethodSignature mi,
                Type[] fnTypeArgs, object?[] prms) {
                if (missing >= methods.Length)
                    throw new StaticException($"Partial function application missing {missing} arguments are not supported");
                var makeFn = methods[missing] ??= typeof(LambdaHelpers)
                                                      .GetMethod($"PartialMissing{missing}",
                                                          BindingFlags.Static | BindingFlags.Public)
                                                  ?? throw new Exception($"Couldn't find partial method for count {missing}");
                return makeFn.MakeGenericMethod(fnTypeArgs).Invoke(null, new object?[] { ast, mi, prms });
            }
        }
    }
    

    public interface IGenericMethodSignature : Reflector.IMethodSignature {
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
        public static readonly Dictionary<(FreezableArray<Type>, MethodSignature), MethodSignature> specializeCache = new();
        
        public override object? Invoke(Reflection2.AST.MethodCall? ast, params object?[] prms) {
            throw new Exception("A generic method signature cannot be invoked");
        }

        public override Ex InvokeEx(Reflection2.AST.MethodCall? ast, params Ex[] args) {
            throw new Exception("A generic method signature cannot be invoked");
        }

        public MethodSignature Specialize(params Type[] t) {
            var typDef = new FreezableArray<Type>(t);
            var specialized = specializeCache.TryGetValue((typDef, this), out var m) ?
                m :
                specializeCache[(typDef, this)] = MethodSignature.Get(Minf.Mi.MakeGenericMethod(t));
            return PartialArgs is { } pargs ? 
                specialized.PartiallyApply(pargs) : 
                specialized;
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
        
        public override object? Invoke(Reflection2.AST.MethodCall? ast, params object?[] prms) {
            throw new Exception(
                "This lifted method signature does not have a specified return type and therefore cannot be invoked");
        }

        public override Ex InvokeEx(Reflection2.AST.MethodCall? ast, params Ex[] args) {
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
        
        public static (TypeDesignation.Dummy sig, Type retType) PartiallyApplyAndLiftParams(int applied, Type t, MethodSignature baseMeth, Dictionary<Type, TypeDesignation.Variable> genericTypeMap) {
            var baseTypes = baseMeth.Params;
            var pTypes = new TypeDesignation[applied + 1];
            for (int ii = 0; ii < applied; ++ii) {
                var bt = baseTypes[ii].Type;
                pTypes[ii] = TypeDesignation.FromType(Reflector.ReflectionData.LiftType(t, bt, out var result) ? result : bt, genericTypeMap);
            }
            
            var retType = ReflectionUtils.GetFuncType(baseMeth.Params.Length - applied + 1)
                .MakeGenericType(PartiallyAppliedFnTypeArgs(applied, baseMeth.Params, baseMeth.ReturnType));
            Reflector.ReflectionData.LiftType(t, retType, out var liftedReturnType);
            pTypes[^1] = TypeDesignation.FromType(liftedReturnType, genericTypeMap);
            return (new TypeDesignation.Dummy(TypeDesignation.Dummy.METHOD_KEY, pTypes), retType);
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
                $"Dynamic instantiation of LiftedMethodSignature<{t.ExRName()},{r.ExRName()}> failed");
        }

        public override MethodSignature PartiallyApply(int args) {
            var (sig, retType) = PartiallyApplyAndLiftParams(args, typeof(T), Original, GenericTypeMap);
            return MakeForReturnType(retType, Original) with {
                PartialArgs = args,
                SharedType = sig
            };
        }
    }
    
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record GenericLiftedMethodSignature<T>(MethodSignature Original, TypeMember.Method Minf, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams), IGenericMethodSignature  {
        public override Type ReturnType => Reflector.Func2Type(typeof(T), base.ReturnType);

        public override object? Invoke(Reflection2.AST.MethodCall? ast, params object?[] prms)
            => throw new Exception("A generic lifted method cannot be invoked");
        
        public override Ex InvokeEx(Reflection2.AST.MethodCall? ast, params Ex[] args) {
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
            return PartialArgs is { } pargs ? 
                (LiftedMethodSignature<T>)method.PartiallyApply(pargs) : 
                method;
        }

        MethodSignature IGenericMethodSignature.Specialize(Type[] t) => Specialize(t);

        public override MethodSignature PartiallyApply(int args) {
            var (sig, _) = PartiallyApplyAndLiftParams(args, typeof(T), Original, GenericTypeMap);
            return this with {
                PartialArgs = args,
                SharedType = sig
            };
        }
    }
    
    //Note that we must eventually specify the R in LiftedMethodSignature in order to ensure that
    // InvokeMiFunced creates a correctly-typed Func<T,R>.
    /// <inheritdoc cref="LiftedMethodSignature"/>
    public record LiftedMethodSignature<T, R>(MethodSignature Original, Reflector.NamedParam[] FuncedParams, Reflector.NamedParam[] BaseParams) 
        : LiftedMethodSignature<T>(Original, FuncedParams, BaseParams) {
        private static readonly Dictionary<MemberInfo, LiftedMethodSignature<T, R>> liftCache = new();
        public override Type ReturnType => typeof(Func<T, R>);

        public override object? Invoke(Reflection2.AST.MethodCall? ast, params object?[] prms) {
            return InvokeMiFunced(ast, prms);
        }
        
        public override Ex InvokeEx(Reflection2.AST.MethodCall? ast, params Ex[] args) {
            throw new Exception("Lifted methods cannot be invoked as expressions");
        }

        public Func<T,R> InvokeMiFunced(Reflection2.AST.MethodCall? ast, object?[] fprms) => 
            //Note: this lambda capture generally prevents using ArrayCache
            bpi => {
                var baseArgs = new object?[PartialArgs ?? BaseParams.Length];
                var writesTo = Member.GetAttribute<AssignsAttribute>()?.Indices ?? Array.Empty<int>();
                foreach (var writeable in writesTo) {
                    DefuncArg(writeable);
                    if (writeable < baseArgs.Length &&
                        Reflection2.Helpers.AssertWriteable(writeable, baseArgs[writeable]!) is { } exc) {
                        if (writesTo.Length == 1 && ast?.Params[writeable] is Reflection2.AST.WeakReference wr && bpi is TExArgCtx tac) {
                            //Dynamic scoped references don't return a writeable expression from the get method,
                            // so we need special handling to write to them
                            return wr.RealizeAsWeakWriteable(tac, setter => {
                                //setter is Ex, the function requires TEx<T>
                                baseArgs[writeable] = Activator.CreateInstance(BaseParams[writeable].Type, setter);
                                //Execute the rest of the base args inside this lambda so we can get caching
                                // on the lexical scope lookup
                                DefuncBaseArgs(except: writeable);
                                return FinalizeCall() as TEx ?? 
                                       throw new StaticException("Couldn't convert writeable workaround internal");
                            }) is R result ? 
                                result : throw new StaticException("Couldn't convert writeable workaround external");
                        }
                        throw ast?.Raise(exc) as Exception ?? exc;
                    }
                }
                DefuncBaseArgs(null);
                return (R)FinalizeCall();
                void DefuncArg(int ii) {
                    //Convert from funced object to base object (eg. TExArgCtx->TEx<float> to TEx<float>)
                    baseArgs[ii] = Reflector.ReflectionData.Defuncify(
                        BaseParams[ii].Type, FuncedParams[ii].Type, fprms[ii]!, bpi!);
                }
                void DefuncBaseArgs(int? except) {
                    for (int ii = 0; ii < baseArgs.Length; ++ii)
                        if (ii != except)
                            DefuncArg(ii);
                            
                }
                object FinalizeCall() => PartialArgs is {} pargs ?
                    LambdaHelpers.MakePartialFunc(Params.Length - pargs, ast, Original, typeof(R).GetGenericArguments(), baseArgs) :
                    Member.Invoke(baseArgs)!;
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