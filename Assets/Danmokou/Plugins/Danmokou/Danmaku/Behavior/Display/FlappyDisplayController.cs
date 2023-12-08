using System;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class FlappyDisplayController : FrameAnimDisplayController {
    public float xSpeedOffset = 5f;

    public override void FaceInDirection(Vector2 delta) {
        delta.x += xSpeedOffset * Math.Max(0.001f, delta.magnitude);
        base.FaceInDirection(delta);
    }
}
}