using System;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public abstract class DisplayController : MonoBehaviour {
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
    public BPY? RotatorF { get; set; }

    protected virtual void Awake() {
        RotatorF = rotator.Get().IntoIfNotNull<BPY>();
    }

    public virtual void ResetV(BehaviorEntity parent) {
        tr = transform;
        beh = parent;
        pb = CreatePB();
        flipX = flipY = false;
        RotatorF = rotator.Get().IntoIfNotNull<BPY>();
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

    public virtual void Animate(AnimationType typ, bool loop, Action? done) {
        throw new Exception("DisplayController has no default animation handling");
    }

    public abstract MaterialPropertyBlock CreatePB();

    //This function is called from a BEH in RegularUpdateRender.
    public virtual void UpdateRender() {
        SetTransform();
        pb.SetFloat(PropConsts.time, time);
    }

    protected virtual Vector3 GetScale => new Vector3(1, 1, 1);

    private void SetTransform() {
        Vector3 scale = GetScale;
        scale.x *= flipX ? -1 : 1;
        scale.y *= flipY ? -1 : 1;
        if (yPosBopPeriod > 0) {
            float yOffset = yPosBopAmplitude * M.Sin(M.TAU * time / yPosBopPeriod);
            tr.localPosition = new Vector3(0, yOffset);
        }
        if (yScaleBopPeriod > 0) {
            scale.y *= 1 + yScaleBopAmplitude * M.Sin(M.TAU * time / yScaleBopPeriod);
        }
        if (RotatorF != null) {
            tr.localEulerAngles = new Vector3(0, 0, RotatorF(beh.rBPI));
        }
        tr.localScale = scale;
    }

    public virtual void SetProperty(int id, float val) => pb.SetFloat(id, val);

    public void SetHueShift(float f) => SetProperty(PropConsts.HueShift, f);

    private void FaceInDirectionRaw(float deg) {
        tr.eulerAngles = new Vector3(0, 0, deg);
    }

    public virtual void FaceInDirection(Vector2 dir) {
        if (rotationMethod != RotationMethod.Manual && dir.x * dir.x + dir.y * dir.y > 0f) {
            FaceInDirectionRaw(M.radDeg * (rotationMethod is RotationMethod.InVelocityDirection ?
                Mathf.Atan2(dir.y, dir.x) :
                rotationMethod is RotationMethod.VelocityDirectionPlus90 ?
                    Mathf.Atan2(dir.x, -dir.y) :
                    rotationMethod is RotationMethod.VelocityDirectionMinus90 ?
                        Mathf.Atan2(-dir.x, dir.y) :
                        0));
        }
    }

    protected static readonly Action noop = () => { };


    protected void SetFlip(bool flipx, bool flipy) {
        flipX = flipx;
        flipY = flipy;
    }
}
}
