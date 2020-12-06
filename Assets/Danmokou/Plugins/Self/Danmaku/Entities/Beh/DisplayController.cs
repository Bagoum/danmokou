using System;
using System.Collections;
using System.Collections.Generic;
using Danmaku;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

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
    protected Transform tr { get; private set; }
    protected MaterialPropertyBlock pb { get; private set; }
    protected BehaviorEntity beh { get; private set; }

    public RFloat yPosBopPeriod;
    public RFloat yPosBopAmplitude;
    public RFloat yScaleBopPeriod;
    public RFloat yScaleBopAmplitude;

    public RString rotator;
    [CanBeNull] public BPY RotatorF { get; set; }

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

    public virtual void SetSprite([CanBeNull] Sprite s) {
        throw new Exception("DisplayController has no default handling for sprite set");
    }
    public virtual void FadeSpriteOpacity(BPY fader01, float over, ICancellee cT, Action done) {
        throw new Exception("DisplayController has no default handling for fading sprite opacity");
    }

    public virtual void Animate(AnimationType typ, bool loop, [CanBeNull] Action done) {
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


    private void FaceInDirectionRaw(float deg) {
        tr.eulerAngles = new Vector3(0, 0, deg);
    }

    public virtual void FaceInDirection(Vector2 dir) {
        if (rotationMethod != RotationMethod.Manual && dir.x * dir.x + dir.y * dir.y > 0f) {
            if (rotationMethod == RotationMethod.InVelocityDirection) {
                FaceInDirectionRaw(Mathf.Atan2(dir.y, dir.x) * M.radDeg);
            } else if (rotationMethod == RotationMethod.VelocityDirectionPlus90) {
                FaceInDirectionRaw(Mathf.Atan2(dir.x, -dir.y) * M.radDeg);
            } else if (rotationMethod == RotationMethod.VelocityDirectionMinus90) {
                FaceInDirectionRaw(Mathf.Atan2(-dir.x, dir.y) * M.radDeg);
            }
        }
    }
    
    protected static readonly Action noop = () => { };
    
    
    protected void SetFlip(bool flipx, bool flipy) {
        flipX = flipx;
        flipY = flipy;
    }
}