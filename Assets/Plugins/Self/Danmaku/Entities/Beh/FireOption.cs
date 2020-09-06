using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Danmaku;
using DMath;
using UnityEngine;

public class FireOption : BehaviorEntity {
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

    private static readonly Dictionary<int, FireOption> optionsByIndex = new Dictionary<int, FireOption>();
    protected override void Awake() {
        base.Awake();
        bpi = new ParametricInfo(bpi.loc, findex, bpi.id, bpi.t);
        original_angle = 0; //Shoot up by default
        freeOffset = offsetFree.Into<TP>();
        focusOffset = offsetFocus.Into<TP>();
        optionsByIndex[findex] = this;
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

    protected override void OnDisable() {
        optionsByIndex.Remove(findex);
        base.OnDisable();
    }

    public static Vector2 OptionLocation(int index) =>
        optionsByIndex.TryGetValue(index, out var v) ? (Vector2)v.tr.position : Vector2.zero;
    
    public static float OptionAngle(int index) =>
        optionsByIndex.TryGetValue(index, out var v) ? v.original_angle : 0f;

    public static readonly ExFunction optionLocation = ExUtils.Wrap<FireOption>("OptionLocation", typeof(int));
    public static readonly ExFunction optionAngle = ExUtils.Wrap<FireOption>("OptionAngle", typeof(int));
}