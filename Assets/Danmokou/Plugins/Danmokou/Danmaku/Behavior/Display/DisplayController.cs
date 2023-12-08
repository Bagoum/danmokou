using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.Behavior.Display {
public abstract class DisplayController : MonoBehaviour, IBehaviorEntityDependent {
    public enum RotationMethod {
        Manual,
        InVelocityDirection,
        VelocityDirectionPlus90,
        VelocityDirectionMinus90
    }

    public RotationMethod rotationMethod;

    protected bool flipX;
    protected bool flipY;

    protected float time => beh.rBPI.t;
    protected Transform tr { get; private set; } = null!;
    protected MaterialPropertyBlock pb { get; private set; } = null!;
    protected BehaviorEntity beh { get; private set; } = null!;
    protected ParametricInfo BPI => beh.rBPI;

    public RFloat yPosBopPeriod = null!;
    public RFloat yPosBopAmplitude = null!;
    public RFloat yScaleBopPeriod = null!;
    public RFloat yScaleBopAmplitude = null!;

    [ReflectInto(typeof(BPY))]
    public RString rotator = null!;
    private BPY? rotatorF;
    private float lastScalerValue = 1f;
    private Vector3 lastScale = Vector3.one;

    protected virtual void Awake() {
        rotatorF = rotator.Get().IntoIfNotNull<BPY>();
    }

    public virtual void LinkAndReset(BehaviorEntity parent) {
        tr = transform;
        beh = parent;
        beh.LinkDependentUpdater(this);
        pb = CreatePB();
        flipX = flipY = false;
        rotatorF = rotator.Get().IntoIfNotNull<BPY>();
        SetTransform();
        Show();
    }

    public abstract void SetMaterial(Material mat);

    public virtual void Show() { }
    public virtual void Hide() { }

    public virtual void UpdateStyle(BehaviorEntity.BEHStyleMetadata style) { }

    public virtual void SetSortingOrder(int x) { }

    public virtual void SetSprite(Sprite? s) {
        throw new Exception("DisplayController has no default handling for sprite set");
    }

    public virtual void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) {
        throw new Exception("DisplayController has no default handling for fading sprite opacity");
    }

    public void Scale(BPY scaler, float over, ICancellee cT, Action done) {
        var tbpi = ParametricInfo.WithRandomId(beh.rBPI.loc, beh.rBPI.index);
        lastScalerValue = scaler(tbpi);
        beh.RunRIEnumerator(_Scale(scaler, tbpi, over, cT, done));
    }

    public virtual void Animate(AnimationType typ, bool loop, Action? done) {
        throw new Exception("DisplayController has no default animation handling");
    }

    public abstract MaterialPropertyBlock CreatePB();

    //This function is called from a BEH in RegularUpdateRender.
    /// <summary>
    /// Update the rendering for this display. Called from <see cref="BehaviorEntity.UpdateRendering"/>.
    /// </summary>
    /// <param name="isFirstFrame">True if this is the first frame update for the object.
    /// It is sometimes efficient to skip rendering updates when !ETime.LastUpdateForScreen,
    /// but rendering updates should not be skipped on the first frame.</param>
    public virtual void UpdateRender(bool isFirstFrame) {
        SetTransform();
        if (ETime.LastUpdateForScreen || isFirstFrame) {
            pb.SetFloat(PropConsts.time, time);
        }
    }

    protected virtual Vector3 GetScale => new(1, 1, 1);

    private void SetTransform() {
        Vector3 scale = GetScale;
        scale.x *= flipX ? -1 : 1;
        scale.y *= flipY ? -1 : 1;
        scale *= lastScalerValue;
        if (yPosBopPeriod > 0) {
            float yOffset = yPosBopAmplitude * M.Sin(BMath.TAU * time / yPosBopPeriod);
            tr.localPosition = new Vector3(0, yOffset);
        }
        if (yScaleBopPeriod > 0) {
            scale.y *= 1 + yScaleBopAmplitude * M.Sin(BMath.TAU * time / yScaleBopPeriod);
        }
        if (rotatorF != null) {
            tr.localEulerAngles = new Vector3(0, 0, rotatorF(beh.rBPI));
        }
        if (scale != lastScale)
            tr.localScale = lastScale = scale;
    }

    public virtual void SetProperty(int id, float val) => pb.SetFloat(id, val);

    public void SetHueShift(float f) => SetProperty(PropConsts.HueShift, f);

    private void FaceInDirectionRaw(float deg) {
        tr.eulerAngles = new Vector3(0, 0, deg);
    }

    public virtual void FaceInDirection(Vector2 delta) {
        if (rotationMethod != RotationMethod.Manual && delta.x * delta.x + delta.y * delta.y > 0f) {
            FaceInDirectionRaw(BMath.radDeg * (rotationMethod switch {
                RotationMethod.InVelocityDirection => (float)Math.Atan2(delta.y, delta.x),
                RotationMethod.VelocityDirectionPlus90 => (float)Math.Atan2(delta.x, -delta.y),
                RotationMethod.VelocityDirectionMinus90 => (float)Math.Atan2(-delta.x, delta.y),
                _ => 0
            }));
        }
    }

    protected static readonly Action noop = () => { };


    protected void SetFlip(bool flipx, bool flipy) {
        flipX = flipx;
        flipY = flipy;
    }
    
    private IEnumerator _Scale(BPY scaler, ParametricInfo tbpi, float over, ICancellee cT, Action done) {
        if (cT.Cancelled) { done(); yield break; }
        for (tbpi.t = 0f; tbpi.t < over - ETime.FRAME_YIELD; tbpi.t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.Cancelled) { break; } //Set to target and then leave
            tbpi.loc = beh.rBPI.loc;
            lastScalerValue = scaler(tbpi);
        }
        tbpi.t = over;
        lastScalerValue = scaler(tbpi);
        done();
    }
}
}
