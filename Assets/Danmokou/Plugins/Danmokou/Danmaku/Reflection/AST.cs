
using System;
using BagoumLib.Reflection;
using static Danmokou.Reflection.Reflector;

namespace Danmokou.Reflection {

public abstract record AST {
    public abstract object EvaluateObject();
}

public abstract record AST<T> : AST {
    public override object EvaluateObject() => Evaluate()!;
    public abstract T Evaluate();

    public record MethodInvoke(MethodSignature Method, AST[] Params) : AST<T> {
        public override T Evaluate() {
            var prms = new object[Params.Length];
            for (int ii = 0; ii < prms.Length; ++ii)
                prms[ii] = Params[ii].EvaluateObject();
            return Method.Mi.Invoke(null, prms) switch {
                T t => t,
                { } x => throw new StaticException(
                    $"AST method invocation for {Method.TypeEnclosedName} returned object of type {x.GetType()} (expected {typeof(T)}")
            };
        }
    }

    public record FuncifiedMethodInvoke(MethodSignature Method, AST[] Params) : AST<T> {
        public override T Evaluate() {
            return default!;
        }
    }
    
}

}