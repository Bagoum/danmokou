using System;
using UnityEngine;

namespace Danmokou.SRPG {

public class Pushable {
    public float MaxDisplace { get; }
    public float Deceleration { get; }
    private float MaxVelocity { get; }
    public Vector2 CurrDisplace { get; private set; }
    private Vector2 currVelocity;
    public Pushable(float maxDisplace, float maxVel, float deceleration) {
        this.MaxDisplace = maxDisplace;
        this.MaxVelocity = maxVel;
        this.Deceleration = deceleration;
    }

    public void Push(Vector2 velocity) {
        currVelocity += velocity;
        if (currVelocity.magnitude > MaxVelocity)
            currVelocity = currVelocity.normalized * MaxVelocity;
    }

    public void DoUpdate(float dT) {
        CurrDisplace += currVelocity * dT;
        if (CurrDisplace.magnitude > MaxDisplace)
            CurrDisplace = MaxDisplace * CurrDisplace.normalized;
        currVelocity -= Deceleration * dT * CurrDisplace.normalized;
        if (Vector2.Dot(CurrDisplace, currVelocity) < 0) {
            var cdm = CurrDisplace.magnitude;
            if (cdm < 0.01 * MaxDisplace)
                currVelocity = Vector2.zero;
            else
                currVelocity *= MathF.Pow(cdm/MaxDisplace, dT/0.3f);
        } 
    }
}
}