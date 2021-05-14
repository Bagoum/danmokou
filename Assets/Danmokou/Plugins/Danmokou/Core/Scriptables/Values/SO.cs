using System;
using UnityEngine;

namespace Danmokou.Scriptables {
public class SO<T> : ScriptableObject, ISerializationCallbackReceiver {

    public T value = default!;
    [NonSerialized] protected T runtimeValue = default!; //Note that persistence occurs only in the editor. 

    public static implicit operator T(SO<T> t) {
        return t.runtimeValue;
    }

    public T Get() {
        return runtimeValue;
    }

    public void Set(T t) {
        runtimeValue = t;
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize() {
        runtimeValue = value;
    }
}
}
