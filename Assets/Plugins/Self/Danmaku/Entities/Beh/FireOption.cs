using System;
using System.Collections;
using System.Threading;
using Danmaku;
using DMath;
using UnityEngine;

public class FireOption : BehaviorEntity {
    public static bool FiringAndAllowed => InputManager.IsFiring && PlayerInput.AllowPlayerInput;
    public string offsetFree;
    public string offsetFocus;
    private TP freeOffset;
    private TP focusOffset;
    public float offsetLerpTime;
    /// <summary>
    /// =0 when in free offset, =1 when in focus
    /// </summary>
    private float currLerpRatio;
    public int findex;
    public BehaviorEntity freeFirer;
    public BehaviorEntity focusFirer;
    protected override void Awake() {
        base.Awake();
        bpi = new ParametricInfo(bpi.loc, findex, bpi.id, bpi.t);
        original_angle = 0; //Shoot up by default
        freeOffset = offsetFree.Into<TP>();
        focusOffset = offsetFocus.Into<TP>();
    }

    protected override void RegularUpdateMove() {
        //Shot files are oriented upwards by default
        base.RegularUpdateMove();
        if (InputManager.FiringAngle.HasValue) original_angle = InputManager.FiringAngle.Value - 90;
        var lerpDir = PlayerInput.IsFocus ? 1 : -1;
        currLerpRatio = Mathf.Clamp01(currLerpRatio + lerpDir * ETime.FRAME_TIME / offsetLerpTime);
        tr.localPosition = M.RotateVectorDeg(freeOffset(bpi) * (1 - currLerpRatio) + focusOffset(bpi) * currLerpRatio, original_angle);
    }
    public override int UpdatePriority => UpdatePriorities.PLAYER2;
}