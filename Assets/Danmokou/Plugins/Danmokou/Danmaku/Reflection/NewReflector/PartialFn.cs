using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using BagoumLib.Unification;
using Danmokou.Expressions;
using Danmokou.Reflection;
using static BagoumLib.Unification.TypeDesignation;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection2 {
public static class PartialFn {
    public static Ex Execute(Ex func, IEnumerable<Ex> args) {
        return Ex.Call(func, func.Type.GetMethod("Invoke")!, args);
    }

    public static Ex Execute(Ex func, params Ex[] args) => Execute(func, args as IEnumerable<Ex>);

    public static Ex PartiallyApply(Ex func, IEnumerable<Ex> _applied) {
        var applied = _applied.ToArray();
        if (applied.Length == 0)
            return func;
        //given Func<A,B,C,D...R> and args [A, B],
        // we create a delegate Func<Func<A,B,C,D...R>,A,B,Func<C,D...R>>, then execute that
        var origFnTypes = func.Type.GetGenericArguments();
        if (func.Type.Name.StartsWith("Action"))
            origFnTypes = origFnTypes.Append(typeof(void)).ToArray();
        var delTypes = origFnTypes.Take(applied.Length)
            .Prepend(func.Type)
            .Append(ReflectionUtils.MakeFuncType(origFnTypes.Skip(applied.Length).ToArray()))
            .ToArray();
        var args = new IDelegateArg[delTypes.Length - 1];
        for (int ii = 0; ii < args.Length; ++ii)
            args[ii] = new DelegateArg($"$parg{ii}", delTypes[ii]);
        var delFnType = ReflectionUtils.MakeFuncType(delTypes);
        Func<TExArgCtx, TEx> body = tac => {
            var delayedArgs =  origFnTypes
                .Skip(applied.Length)
                .Take(origFnTypes.Length - 1 - applied.Length)
                .Select((d, i) => Ex.Parameter(d, $"$delayed{i}"))
                .ToArray();
            //This basically creates the lambda (delayed...) => parg0.Invoke(parg1...pargn, delayed...)
            return Ex.Lambda(Execute(tac.GetByName(delTypes[0], "$parg0"),
                    applied.Length.Range()
                        .Select(i => (Ex)tac.GetByName(delTypes[i + 1], $"$parg{i + 1}"))
                        .Concat(delayedArgs)),
                delayedArgs);
        };
        var helper = CompilerHelpers.CompileDelegateMeth.Specialize(delFnType).Invoke(null, body, args)!;
        return Execute(Ex.Constant(helper), applied.Prepend(func));
    }
    
    /// <summary>
    /// Given a function type (A,B,C...)->X, return a partially applied type
    ///     (A,B)->Func&lt;C...X&gt;.
    /// <br/>If prependOriginal is set, returns the general partial application type 
    ///     (Func&lt;A,B,C...X&gt;,A,B)->Func&lt;C...X&gt;.
    /// </summary>
    /// <param name="fn"></param>
    /// <param name="args"></param>
    /// <param name="prependOriginal"></param>
    /// <returns></returns>
    public static Dummy PartiallyApply(Dummy fn, int args, bool prependOriginal) {
        var pfnTypeArgs = fn.Arguments.Skip(args).ToArray();
        var applied = fn.Arguments.Take(args);
        if (prependOriginal)
            applied = applied.Prepend(MakeFuncType(fn.Arguments));
        return Dummy.Method(MakeFuncType(pfnTypeArgs), applied.ToArray());
    }
    
    /// <summary>
    /// Undo <see cref="PartiallyApply(Dummy,int,bool)"/>.
    /// </summary>
    public static Dummy PartiallyUnApply(Dummy fn, int args, bool prependOriginal) {
        var applied = (prependOriginal ? fn.Arguments.Skip(1) : fn.Arguments).Take(args);
        var fnTypArgs = fn.Last.Arguments;
        return Dummy.Method(fnTypArgs[^1],
            applied.Concat(fnTypArgs.Take(fnTypArgs.Length - 1)).ToArray());
    }

    public static Known MakeFuncType(TypeDesignation[] typeArgs) {
        if (typeArgs[^1].Resolve().LeftOrNull == typeof(void))
            return new Known(ReflectionUtils.GetActionType(typeArgs.Length - 1),
                typeArgs.Take(typeArgs.Length - 1).ToArray());
        return new Known(ReflectionUtils.GetFuncType(typeArgs.Length), typeArgs);
    }
}
}