using System;
using Danmokou.Core;
using JetBrains.Annotations;

namespace Danmokou.DMath {
public class Lerpifier<T> {
    private readonly Func<T, T, float, T> lerper;
    private readonly Func<T> targetValue;
    private readonly float lerpTime;
    private T lastSourceValue;
    private T lastTargetValue;
    public T NextValue { get; private set; }
    private float elapsed;
    
    private readonly Evented<T> ev;
    public Events.IEvent<T> OnChange => ev.OnChange;

    public Lerpifier(Func<T, T, float, T> lerper, Func<T> targetValue, float lerpTime, Lerpifier<T>? inheritListeners=null) {
        this.lerper = lerper;
        this.targetValue = targetValue;
        this.lerpTime = lerpTime;
        this.lastSourceValue = lastTargetValue = NextValue = targetValue();
        this.elapsed = this.lerpTime;
        ev = new Evented<T>(NextValue, inheritListeners?.ev);
    }

    public void HardReset() {
        this.lastSourceValue = lastTargetValue = NextValue = targetValue();
        this.elapsed = this.lerpTime;
    }

    public void Update(float dt) {
        var nextTarget = targetValue();
        if (!nextTarget!.Equals(lastTargetValue)) {
            lastSourceValue = NextValue;
            lastTargetValue = nextTarget;
            elapsed = 0;
        }
        elapsed += dt;
        var prev = NextValue;
        NextValue = elapsed >= lerpTime ? 
            lastTargetValue : 
            lerper(lastSourceValue, lastTargetValue, elapsed / lerpTime);
        if (!prev!.Equals(NextValue))
            ev.Value = NextValue;
    }
}
}