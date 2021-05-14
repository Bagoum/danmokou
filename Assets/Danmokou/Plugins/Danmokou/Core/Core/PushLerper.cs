using System;
using UnityEngine;

namespace Danmokou.Core {
public class PushLerper<T> {
    private readonly float lerpTime;
    private readonly Func<T, T, float, T> lerper;

    private bool set = false;
    private T lastValue;
    private T nextValue;
    private float elapsed;

    public T Value => elapsed > lerpTime ? nextValue : lerper(lastValue, nextValue, elapsed / lerpTime);
    
    public PushLerper(float lerpTime, Func<T, T, float, T> lerper) {
        this.lastValue = nextValue = default!;
        this.lerpTime = lerpTime;
        this.lerper = lerper;
    }

    public void Push(T targetValue) {
        if (set) {
            lastValue = Value;
            nextValue = targetValue;
        } else {
            lastValue = nextValue = targetValue;
        }
        set = true;
        elapsed = 0;
    }

    public void Update(float dT) {
        elapsed += dT;
    }

    public static implicit operator T(PushLerper<T> pl) => pl.Value;
}
}