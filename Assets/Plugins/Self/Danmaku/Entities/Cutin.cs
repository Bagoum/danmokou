using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Danmaku;
using JetBrains.Annotations;

public class Cutin : RegularUpdater {
    protected MaterialPropertyBlock pb;
    private SpriteRenderer sr;
    private BehaviorEntity beh;
    public float recordPositionEvery = 1f;
    private float recordCtr;
    [Header("Afterimages")] [CanBeNull] public GameObject afterImage;
    [Tooltip("Velocity of an AI is by default the velocity of the BEH at time of creation")]
    public float AIVelocityRotationDeg;
    public float AIVelocitySpeedRatio;
    
    protected virtual void Awake() {
        beh = GetComponent<BehaviorEntity>();
        sr = GetComponent<SpriteRenderer>();
        pb = new MaterialPropertyBlock();
    }

    public override void RegularUpdate() {
        recordCtr += ETime.FRAME_TIME;
        if (recordCtr > recordPositionEvery) {
            recordCtr -= recordPositionEvery;
            if (afterImage != null) {
                CutinGhost cg = Instantiate(afterImage).GetComponent<CutinGhost>();
                cg.Initialize(beh.GlobalPosition(),
                    DMath.M.RotateVectorDeg(beh.LastDelta * (AIVelocitySpeedRatio / ETime.FRAME_TIME),
                        AIVelocityRotationDeg));
            }
        }
    }


    [ContextMenu("Editor display")]
    public void EditorDisplay() {
        Awake();
        MainCamera.LoadInEditor();
    }
}