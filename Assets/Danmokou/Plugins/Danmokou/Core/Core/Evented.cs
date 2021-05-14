using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;

namespace Danmokou.Core {
public class Evented<T> {

    private T value;
    
    public T Value {
        get => value;
        set => OnChange.Publish(this.value = value);
    }
    private readonly Events.Event<T> onChange;
    public Events.IEvent<T> OnChange => onChange;

    public Evented(T val, Evented<T>? inheritListeners=null) {
        value = val;
        onChange = new Events.Event<T>(inheritListeners?.onChange);
        onChange.Publish(value);
    }

    public static implicit operator T(Evented<T> evo) => evo.value;
}
}