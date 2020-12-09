using System;
using UnityEngine;

namespace DMK.Scriptables {
public class SO<T> : ScriptableObject, ISerializationCallbackReceiver {

    public T value;
    [NonSerialized] protected T runtimeValue; //Note that persistence occurs only in the editor. 

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
