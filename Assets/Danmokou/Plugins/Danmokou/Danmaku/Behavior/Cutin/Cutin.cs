using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Danmokou.Core;
using Danmokou.Pooling;
using JetBrains.Annotations;

namespace Danmokou.Behavior.Display {
public class Cutin : RegularUpdater {
    private BehaviorEntity beh = null!;
    public float recordPositionEvery = 1f;
    private float recordCtr;
    [Header("Ghosts")]
    [Tooltip("Velocity of an AI is by default the velocity of the BEH at time of creation")]
    public float AIVelocityRotationDeg;
    public float AIVelocitySpeedRatio;

    [Serializable]
    public struct GhostConfig {
        public Sprite sprite;
        public float ttl;
        public Color startColor;
        public Color endColor;
        public Vector2 scale;
        public Vector2 blurRad;
        public float blurMaxAt;
    }

    public GhostConfig ghost;
    
    protected virtual void Awake() {
        beh = GetComponent<BehaviorEntity>();
    }

    public override void RegularUpdate() {
        recordCtr += ETime.FRAME_TIME;
        if (recordCtr > recordPositionEvery) {
            recordCtr -= recordPositionEvery;
            GhostPooler.Request(beh.GlobalPosition(),
                DMath.M.RotateVectorDeg(beh.LastDelta * (AIVelocitySpeedRatio / ETime.FRAME_TIME),
                    AIVelocityRotationDeg), ghost);
        }
    }
}
}