using System;
using System.Collections.Generic;
using System.Reflection;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.Reflection;

namespace Danmokou.UI.XML {
/// <summary>
/// Bind to an arbitrary field or property on a model or view model.
/// </summary>
public class PropTwoWayBinder<T> : TwoWayBinder<T> {
    private static readonly Dictionary<(Type, string), TypeMember.Writeable> cache = new();
    private readonly Delayed<object> model;
    private readonly TypeMember.Writeable property;
    protected override T GetInner() => (T)property.InvokeInst(model.Value)!;
    protected override void SetInner(T value) => property.SetInst(model.Value, value!);
    
    public PropTwoWayBinder(IUIViewModel vm, string prop) : this(vm, prop, vm) { }

    public PropTwoWayBinder(object model, string prop, IUIViewModel? vm) : base(vm) {
        this.model = model;
        this.property = FindMember(model.GetType(), prop);
    }
    public PropTwoWayBinder(Func<object> model, string prop, IUIViewModel? vm) : base(vm) {
        this.model = model;
        this.property = FindMember(model().GetType(), prop);
    }

    public static TypeMember.Writeable FindMember(Type t, string prop) {
        if (cache.TryGetValue((t, prop), out var mem))
            return mem;
        var members = t.GetMember(prop, BindingFlags.Instance | BindingFlags.Public);
        if (members.Length != 1)
            throw new Exception($"Unique member not defined for {t.RName()}.{prop}");
        return cache[(t, prop)] = TypeMember.MakeWriteable(members[0]);
    }
}

}