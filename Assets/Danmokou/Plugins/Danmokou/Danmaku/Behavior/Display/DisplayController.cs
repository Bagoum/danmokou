using System;
using System.Collections;
using BagoumLib;
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
[Serializable]
public class RotationMethod {
    public enum Strategy {
        Manual,
        InVelocityDirection,
        LeanByVelocity,
    }

    public Strategy type = Strategy.Manual;
    public ManualConfig Manual = new();
    public InVelocityDirectionConfig InVelocityDirection = new();
    public LeanByVelocityConfig LeanByVelocity = new();

    public RotationConfig Active => type switch {
        Strategy.Manual => Manual,
        Strategy.InVelocityDirection => InVelocityDirection,
        _ => LeanByVelocity
    };

    public abstract class RotationConfig {
        public abstract float? GetRotation(bool isFlipX, Vector2 delta);
    }

    [Serializable]
    public class ManualConfig : RotationConfig {
        public override float? GetRotation(bool isFlipX, Vector2 delta) => null;
    }

    [Serializable]
    public class InVelocityDirectionConfig : RotationConfig {
        public float offset = 0f;
        public override float? GetRotation(bool isFlipX, Vector2 delta) {
            var rot = BMath.radDeg * (float)Math.Atan2(delta.y, delta.x) + offset;
            if (isFlipX)
                return rot - 180;
            return rot;
        }
    }

    [Serializable]
    public class LeanByVelocityConfig : RotationConfig {
        public bool xAxis = true;
        public float multiplier = 8;
        public float rotationOffset = 0;
        public Vector2 rotationLimit = new(-30, 30);
            
        public override float? GetRotation(bool isFlipX, Vector2 delta) {
            var velocity = (xAxis ? delta.x : delta.y) / ETime.FRAME_TIME;
            return M.Clamp(rotationLimit.x, rotationLimit.y, velocity * multiplier + rotationOffset);
        }
    }
}

public abstract class DisplayController : MonoBehaviour, IBehaviorEntityDependent {

    public RotationMethod rotationMethod = new();

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
    private Vector3 initialScale;
    private Vector3 lastScale = Vector3.one;

    protected virtual void Awake() {
        rotatorF = rotator.Get().IntoIfNotNull<BPY>();
    }

    public virtual void LinkAndReset(BehaviorEntity parent) {
        tr = transform;
        initialScale = lastScale = tr.localScale;
        beh = parent;
        beh.LinkDependentUpdater(this);
        pb = CreatePB();
        flipX = flipY = false;
        rotatorF = rotator.Get().IntoIfNotNull<BPY>();
        SetTransform();
        Show();
    }

    public void Died() {
        tr.localScale = initialScale;
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

    protected virtual Vector3 BaseScale => initialScale;

    private void SetTransform() {
        Vector3 scale = BaseScale;
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
        if (delta.x * delta.x + delta.y * delta.y > 0f && 
            rotationMethod.Active.GetRotation(flipX, delta).Try(out var rot)) {
            FaceInDirectionRaw(rot);
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
