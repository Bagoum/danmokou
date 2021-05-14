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
    private float timeSinceUpdate;
    
    private readonly Evented<T> ev;
    public Events.IEvent<T> OnChange => ev.OnChange;

    public Lerpifier(Func<T, T, float, T> lerper, Func<T> targetValue, float lerpTime, Lerpifier<T>? inheritListeners=null) {
        this.lerper = lerper;
        this.targetValue = targetValue;
        this.lerpTime = lerpTime;
        this.lastSourceValue = lastTargetValue = NextValue = targetValue();
        this.timeSinceUpdate = this.lerpTime;
        ev = new Evented<T>(NextValue, inheritListeners?.ev);
    }

    public void HardReset() {
        this.lastSourceValue = lastTargetValue = NextValue = targetValue();
        this.timeSinceUpdate = this.lerpTime;
    }

    /// <summary>
    /// Returns true if the value changed.
    /// </summary>
    public bool Update(float dt) {
        var nextTarget = targetValue();
        if (!nextTarget!.Equals(lastTargetValue)) {
            lastSourceValue = NextValue;
            lastTargetValue = nextTarget;
            timeSinceUpdate = 0;
        }
        timeSinceUpdate += dt;
        var prev = NextValue;
        NextValue = timeSinceUpdate >= lerpTime ? 
            lastTargetValue : 
            lerper(lastSourceValue, lastTargetValue, timeSinceUpdate / lerpTime);
        bool changed = !prev!.Equals(NextValue);
        if (changed)
            ev.Value = NextValue;
        return changed;
    }
}
}