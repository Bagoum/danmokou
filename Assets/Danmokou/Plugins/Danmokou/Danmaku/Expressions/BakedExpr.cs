using System;
using BagoumLib.Reflection;
using Danmokou.Reflection2;
using JetBrains.Annotations;

namespace Danmokou.Expressions {
[UsedImplicitly]
public class BakedExpr<F> {
    private readonly Func<object[], F> generator;
    private F? _func;
    public F Func => _func ?? throw new Exception("Baked script function not yet loaded");
    
    public BakedExpr(Func<object[], F> generator) {
        this.generator = generator;
    }

    public F Load(object[] args) {
        return _func = generator(args);
    }
}
}