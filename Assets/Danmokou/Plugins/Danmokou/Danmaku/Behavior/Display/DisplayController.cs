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
    /// <summary>
    /// The BehaviorEntity which this display controller is dependent on.
    /// <br/>If this display is an element of a <see cref="IMultiDisplayController"/>, then this should be null.
    /// </summary>
    [field:SerializeField]
    public BehaviorEntity Beh { get; protected set; } = null!;
    private IMultiDisplayController? container;
    public RotationMethod rotationMethod = new();

    protected bool flipX;
    protected bool flipY;

    protected float time => Beh.rBPI.t;
    private float bopTimeOffset = 0;
    protected Transform tr { get; private set; } = null!;
    protected MaterialPropertyBlock pb { get; private set; } = null!;
    protected ParametricInfo BPI => Beh.rBPI;
    
    public RFloat yPosBopPeriod = null!;
    public RFloat yPosBopAmplitude = null!;
    public RFloat yScaleBopPeriod = null!;
    public RFloat yScaleBopAmplitude = null!;
    public bool randomizeBopTime = true;

    [ReflectInto(typeof(BPY))]
    public RString rotator = null!;
    private BPY? rotatorF;
    private float lastScalerValue = 1f;
    private Vector3 initialScale;
    private Vector3 lastScale = Vector3.one;

    protected virtual void Awake() {
        if (Beh != null && container is null)
            Beh.LinkDependentUpdater(this);
        if (randomizeBopTime)
            bopTimeOffset = RNG.GetFloatOffFrame(0, Math.Max(yScaleBopPeriod, yPosBopPeriod));
    }

    public void IsPartOf(IMultiDisplayController controller) {
        if (Beh != null)
            throw new Exception($"Display controller {gameObject.name} is dependent on {controller}, " +
                                $"but has the {nameof(Beh)} field set");
        Beh = controller.Beh;
        container = controller;
    }

    public virtual void OnLinkOrResetValues(bool isLink) {
        //OnLinkOrResetValues can be called before Awake for components of IMultiDisplayController
        if (isLink) {
            tr = transform;
            initialScale = lastScale = tr.localScale;
            rotatorF = rotator.Get().IntoIfNotNull<BPY>();
        }
        pb = new();
        SetTransform();
        Show();
    }

    public virtual void Culled(bool allowFinalize, Action done) {
        flipX = flipY = false;
        tr.localScale = lastScale = initialScale;
        Hide();
        done();
    }

    public abstract void SetMaterial(Material mat);

    public virtual void Show() { }
    public virtual void Hide() { }

    public virtual void StyleChanged(BehaviorEntity.StyleMetadata style) { }

    public virtual void SetSortingOrder(int x) { }

    public virtual void SetSprite(Sprite? s) {
        throw new Exception("DisplayController has no default handling for sprite set");
    }

    public virtual void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) {
        throw new Exception("DisplayController has no default handling for fading sprite opacity");
    }

    public void Scale(BPY scaler, float over, ICancellee cT, Action done) {
        var tbpi = Beh.rBPI;
        tbpi.t = 0;
        lastScalerValue = scaler(tbpi);
        Beh.RunRIEnumerator(_Scale(scaler, tbpi, over, cT, done));
    }

    public virtual void Animate(AnimationType typ, bool loop, Action? done) {
        throw new Exception("DisplayController has no default animation handling");
    }

    public virtual void OnRender(bool isFirstFrame, Vector2 lastDesiredDelta) {
        FaceInDirection(lastDesiredDelta);
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
        if (yPosBopPeriod > 0 && yPosBopAmplitude > 0) {
            float yOffset = yPosBopAmplitude * M.Sin(BMath.TAU * (time+bopTimeOffset) / yPosBopPeriod);
            tr.localPosition = new Vector3(0, yOffset);
        }
        if (yScaleBopPeriod > 0 && yScaleBopAmplitude > 0) {
            scale.y *= 1 + yScaleBopAmplitude * M.Sin(BMath.TAU * (time+bopTimeOffset) / yScaleBopPeriod);
        }
        if (rotatorF != null) {
            tr.localEulerAngles = new Vector3(0, 0, rotatorF(Beh.rBPI));
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

    public void SetFlip(bool flipx, bool flipy) {
        flipX = flipx;
        flipY = flipy;
    }
    
    private IEnumerator _Scale(BPY scaler, ParametricInfo tbpi, float over, ICancellee cT, Action done) {
        if (cT.Cancelled) { done(); yield break; }
        for (tbpi.t = 0f; tbpi.t < over - ETime.FRAME_YIELD; tbpi.t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.Cancelled) { break; } //Set to target and then leave
            tbpi.loc = Beh.rBPI.loc;
            lastScalerValue = scaler(tbpi);
        }
        tbpi.t = over;
        lastScalerValue = scaler(tbpi);
        done();
    }
}
}
