using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Behavior {
public class FireOption : BehaviorEntity {
    private SpriteRenderer sr = null!; //Using this instead of DisplayController for now
    [ReflectInto(typeof(TP3))] [TextArea(3, 8)] public string offsetFree = null!;
    [ReflectInto(typeof(TP3))] [TextArea(3, 8)] public string offsetFocus = null!;
    [ReflectInto(typeof(TP3))] public string[] powerOffsetFree = null!;
    [ReflectInto(typeof(TP3))] public string[] powerOffsetFocus = null!;
    public float freeOffsetMul = 1f;
    public float focusOffsetMul = 1f;
    [ReflectInto(typeof(BPY))] [TextArea(3, 8)] public string opacityFree = null!;
    [ReflectInto(typeof(BPY))] [TextArea(3, 8)] public string opacityFocus = null!;
    /// <summary>
    /// Visible rotation. Does not affect firing angle.
    /// </summary>
    [ReflectInto(typeof(BPY))] public string spriteRotation = null!;
    private TP3 freeOffset = null!;
    /// <summary>
    /// Visible rotation. Affects firing angle.
    /// </summary>
    [ReflectInto(typeof(BPY))] public string freeAngleOffset = null!;
    /// <summary>
    /// Visible rotation. Affects firing angle.
    /// </summary>
    [ReflectInto(typeof(BPY))] public string focusAngleOffset = null!;
    private TP3 focusOffset = null!;
    private TP3[] freeOffsetPower = null!;
    private TP3[] focusOffsetPower = null!;
    private bool doOpacity;
    private BPY? freeOpacity;
    private BPY? focusOpacity;
    private BPY? rotator;
    private BPY freeAngle = _ => 0;
    private BPY focusAngle = _ => 0;
    private Color rootColor;
    [FormerlySerializedAs("offsetLerpTime")]
    public float freeFocusLerpTime;
    public float powerLerpTime;
    /// <summary>
    /// =0 when in free offset, =1 when in focus
    /// </summary>
    private float freeFocusLerp;
    private int lastPower = 0;
    private int currPower = 0;
    private float powerLerp = 0.2f;
    public int findex;
    protected override int Findex => findex;
    protected override FiringCtx DefaultFCTX {
        get {
            var f = FiringCtx.New();
            f.firer = this;
            f.playerController = Player;
            return f;
        }
    }
    public PlayerController Player { get; private set; } = null!;

    //Called from initialize instead
    protected override void Awake() { }

    public void Initialize(PlayerController playr) {
        Player = playr;
        //I feel kind of bad about this, but it ensures that PlayerInput is linked before the SM runs.
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
        original_angle = 0; //Shoot up by default
        freeFocusLerp = Player.IsFocus ? 1 : 0;
        freeOffset = ReflWrap<TP3>.Wrap(offsetFree);
        if (string.IsNullOrWhiteSpace(offsetFocus)) offsetFocus = offsetFree;
        focusOffset = ReflWrap<TP3>.Wrap(offsetFocus);
        // ReSharper disable once AssignmentInConditionalExpression
        if ((doOpacity = !string.IsNullOrWhiteSpace(opacityFree))) {
            freeOpacity = ReflWrap<BPY>.Wrap(opacityFree);
            if (string.IsNullOrWhiteSpace(opacityFocus)) opacityFocus = opacityFree;
            focusOpacity = ReflWrap<BPY>.Wrap(opacityFocus);
            rootColor = sr.color;
        }
        freeOffsetPower = powerOffsetFree.Select(ReflWrap<TP3>.Wrap).ToArray();
        focusOffsetPower = powerOffsetFocus.Select(ReflWrap<TP3>.Wrap).ToArray();
        if (!string.IsNullOrWhiteSpace(spriteRotation)) rotator = ReflWrap<BPY>.Wrap(spriteRotation);
        if (!string.IsNullOrWhiteSpace(freeAngleOffset)) freeAngle = ReflWrap<BPY>.Wrap(freeAngleOffset);
        if (!string.IsNullOrWhiteSpace(focusAngleOffset)) focusAngle = ReflWrap<BPY>.Wrap(focusAngleOffset);
        lastPower = currPower = Math.Min(freeOffsetPower.Length - 1, GameManagement.Instance.PowerIndex);
        SetLocation();
    }

    public void Preload() {
        ReflWrap<TP3>.Load(offsetFree);
        ReflWrap<TP3>.Load(offsetFocus);
        ReflWrap<BPY>.Load(opacityFree);
        ReflWrap<BPY>.Load(opacityFocus);
        powerOffsetFree.ForEach(ReflWrap<TP3>.Load);
        powerOffsetFocus.ForEach(ReflWrap<TP3>.Load);
        ReflWrap<BPY>.Load(spriteRotation);
        ReflWrap<BPY>.Load(freeAngleOffset);
        ReflWrap<BPY>.Load(focusAngleOffset);
        StateMachineManager.FromText(behaviorScript);
    }

    private Vector3 SelectByPower(TP3[] powers, TP3 otherwise) {
        if (powers.Length == 0) return otherwise(bpi);
        int index = Math.Min(powers.Length - 1, GameManagement.Instance.PowerIndex);
        if (index != currPower) {
            lastPower = currPower;
            currPower = index;
            powerLerp = 0;
        }
        return Vector3.Lerp(powers[lastPower](bpi), powers[currPower](bpi), powerLerp);
    }

    private Vector3 FreeOffset => SelectByPower(freeOffsetPower, freeOffset) * freeOffsetMul;
    private Vector3 FocusOffset => SelectByPower(focusOffsetPower, focusOffset) * focusOffsetMul;

    private void SetLocation() {
        powerLerp = Mathf.Clamp01(powerLerp + ETime.FRAME_TIME / powerLerpTime);
        var lerpDir = Player.IsFocus ? 1 : -1;
        freeFocusLerp = Mathf.Clamp01(freeFocusLerp + lerpDir * ETime.FRAME_TIME / freeFocusLerpTime);
        original_angle = Mathf.Lerp(freeAngle(bpi), focusAngle(bpi), freeFocusLerp);
        tr.localPosition = Vector3.Lerp(FreeOffset, FocusOffset, freeFocusLerp);
        if (doOpacity) {
            var color = rootColor;
            color.a *= freeOpacity!(bpi) * (1 - freeFocusLerp) + focusOpacity!(bpi) * freeFocusLerp;
            sr.color = color;
        }
        tr.localEulerAngles = new Vector3(0, 0, original_angle + rotator?.Invoke(bpi) ?? 0f);
    }

    protected override void RegularUpdateMove() {
        base.RegularUpdateMove();
        SetLocation();
    }

    public override int UpdatePriority => UpdatePriorities.PLAYER2;
}
}
