using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

//Note: The location of a pather is the location of its latest point. The process of drawing through previous points
// is handled automatically via Unity's TrailRenderer. In older versions of DMK, this process was handled via a manual draw,
// which required keeping the pather transform at its starting position and storing a list of offsets from the starting position.
// This means that pathers *might* support parenting now, but I haven't tried.

namespace Danmokou.Graphics {
[Serializable]
public class PatherRenderCfg : TiledRenderCfg {
    public float lineRadius;
    public float headCutoffRatio = 0.05f;
    public float tailCutoffRatio = 0.05f;
}
/// <summary>
/// A pather remembers the positions it has been in and draws a line through them.
/// </summary>
public class CurvedTileRenderPather : CurvedTileRender {
    //Important implementation note: The centers array is a list of *global* positions (as of v5.1.0).
    //This is for efficiency and simplicity re: the TrailRenderer implementation.
    //Lasers require centers to be a list of local positions so it can be pased as a mesh.
    
    /// = 0.033s
    private const int FramePosCheck = 4;
    private int cL;
    private readonly float lineRadius;
    private float scaledLineRadius;
    private readonly float headCutoffRatio;
    private readonly float tailCutoffRatio;
    private BPY remember = null!;
    private BPY? hueShift;
    private (TP4 black, TP4 white)? recolor;
    private TP4? tinter;
    private Movement movement;
    /// <summary>
    /// The last return value of Velocity.Update. Used for backstepping.
    /// </summary>
    private Vector3 lastDelta;
    private int read_from;
    private ParametricInfo bpi;
    public ref ParametricInfo BPI => ref bpi;
    
    //set in pathtracker.awake
    private Action onCameraCulled = null!;
    private int cullCtr;
    private const int checkCullEvery = 120;

    public Pather Pather { get; private set; } = null!;

    //Note: trailRenderer requires reversing the sprite.
    protected override bool HandleAsMesh => false;
    public readonly TrailRenderer trailR;
    public string? Style => Pather.myStyle.style;

    //player bullets only
    private PlayerBullet? playerBullet;

    public CurvedTileRenderPather(PatherRenderCfg cfg, GameObject obj) : base(obj) {
        lineRadius = cfg.lineRadius;
        trailR = obj.GetComponent<TrailRenderer>();
        tailCutoffRatio = cfg.tailCutoffRatio;
        headCutoffRatio = cfg.headCutoffRatio;
    }
    public void SetYScale(float scale) {
        PersistentYScale = scale;
        scaledLineRadius = lineRadius * scale;
    }
    public void Initialize(Pather _pather, TiledRenderCfg cfg,  Material material, bool isNew, Movement mov, 
        ParametricInfo pi, BPY rememberTime, float maxRememberTime, ref RealizedBehOptions options) {
        Pather = _pather;
        int newTexW = (int) Math.Ceiling(maxRememberTime * ETime.ENGINEFPS_F) + 1;
        base.Initialize(_pather, cfg, material, isNew, false, options.playerBullet != null, newTexW);
        if (_pather.HasParent()) throw new NotImplementedException("Pather cannot be parented");
        movement = mov;
        bpi = pi;
        _ = movement.UpdateZero(ref bpi);
        lastDataIndex = cL = centers.Length;
        remember = rememberTime;
        intersectStatus = SelfIntersectionStatus.RAS;
        read_from = cL;
        //isnonzero = false;
        for (int ii = 0; ii < cL; ++ii) {
            centers[ii] = bpi.loc;
        }
        prevRemember = trailR.time = 0f;
        playerBullet = options.playerBullet;
        bounds = new AABB(movement.rootPos, Vector2.zero);
        
        hueShift = options.hueShift;
        recolor = options.recolor;
        tinter = options.tint;
        //This needs to be reset to zero here to ensure that the value isn't dirty, since hue-shift is always active
        pb.SetFloat(PropConsts.HueShift, hueShift?.Invoke(bpi) ?? 0f);
        pb.SetColor(PropConsts.tint, tinter?.Invoke(bpi) ?? Color.white);
        if (hueShift != null || recolor != null || tinter != null) DontUpdateTimeAfter = M.IntFloatMax;
    }

    public Vector2 GlobalPosition => bpi.loc;

    public void SetCameraCullable(Action onCull) {
        onCameraCulled = onCull;
    }

    private const float CULL_RAD = 4;
    private const float FIRST_CULLCHECK_TIME = 4;
    public bool CullCheck() {
        cullCtr = (cullCtr + 1) % checkCullEvery;
        if (cullCtr == 0 && Pather.myStyle.CameraCullable.Value && bpi.t > FIRST_CULLCHECK_TIME && LocationHelpers.OffPlayableScreenBy(CULL_RAD, centers[read_from])) {
            onCameraCulled();
            return true;
        }
        return false;
    }

    public override void UpdateMovement(float dT) {
        movement.UpdateDeltaAssignDelta(ref bpi, ref lastDelta, dT);
        base.UpdateMovement(dT);
        Pather.SetMovementDelta(lastDelta);
    }

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateGraphics() {
        if (hueShift != null) {
            pb.SetFloat(PropConsts.HueShift, hueShift(bpi));
        }
        if (recolor.Try(out var rc)) {
            pb.SetVector(PropConsts.RecolorB, rc.black(bpi));
            pb.SetVector(PropConsts.RecolorW, rc.white(bpi));
        }
        if (tinter != null) {
            pb.SetVector(PropConsts.tint, tinter(bpi));
        }
    }
    public override void UpdateRender() {
        if (ETime.LastUpdateForScreen) {
            UpdateGraphics();
            tr.localPosition = bpi.loc;
            //trailR.AddPosition(bpi.loc);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (prevRemember != nextRemember) {
                trailR.time = prevRemember = nextRemember;
            }
        }
        base.UpdateRender();
    }

    private float prevRemember = 0f;
    private float nextRemember = 0f;

    private AABB bounds;

    //private bool isnonzero;
    /// <summary>
    /// The oldest index containing direction information.
    /// </summary>
    private int lastDataIndex;

    protected override void UpdateVerts(bool renderRequired) {
        var last = cL - 1;
        
        lastDataIndex = (lastDataIndex > 0) ? lastDataIndex - 1 : 0;
        if (lastDataIndex == last) return; //Need at least two frames to draw

        int remembered = (int) Math.Ceiling((nextRemember = remember(bpi)) * ETime.ENGINEFPS_F);
        if (remembered < 2) remembered = 2;
        if (remembered > cL - lastDataIndex) remembered = cL - lastDataIndex;
        
        read_from = cL - remembered;
        
        float minX = 999f;
        float minY = 999f;
        float maxX = -999f;
        float maxY = -999f;
        float wx, wy;
        for (int ii = 0; ii < texRptWidth; ++ii) {
            /*vertsPtr[ii].loc.x = vertsPtr[ii + 1].loc.x;
            vertsPtr[ii].loc.y = vertsPtr[ii + 1].loc.y;
            vertsPtr[ii + cL].loc.x = vertsPtr[ii + cL + 1].loc.x;
            vertsPtr[ii + cL].loc.y = vertsPtr[ii + cL + 1].loc.y;*/
            centers[ii].x = wx = centers[ii + 1].x;
            centers[ii].y = wy = centers[ii + 1].y;
            if (ii >= read_from) {
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wy < minY) minY = wy;
                if (wy > maxY) maxY = wy;
            }
        }
        bounds = new AABB(minX, maxX, minY, maxY);

        centers[last] = bpi.loc;
    }

    private const float BACKSTEP = 2f;

    public bool ComputeCircleCollision(Vector2 location, float radius, int cutTail, int cutHead, out Vector2 collisionLocation) {
        if (CollisionMath.CircleOnAABB(in bounds, in location.x, in location.y, radius + scaledLineRadius)
            && CollisionMath.CircleOnSegments(location, radius, Vector2.zero,
                centers, read_from + cutTail, 1, cL - cutHead, scaledLineRadius, 1, 0, out var segment)) {
            collisionLocation = centers[segment];
            return true;
        } else {
            collisionLocation = Vector2.zero;
            return false;
        }
    }
    
    public CollisionResult ComputeGrazeCollision(Hurtbox hb, int cutTail, int cutHead, out Vector2 collisionLocation) {
        if (CollisionMath.CircleOnAABB(in bounds, in hb.x, in hb.y, hb.grazeRadius + scaledLineRadius)) {
            var coll = CollisionMath.GrazeCircleOnSegments(in hb, Vector2.zero, 
                centers, read_from + cutTail, 1, cL - cutHead, scaledLineRadius, 1, 0, out var segment);
            collisionLocation = centers[segment];
            if (coll.graze && !Pather.GrazeAllowed)
                return coll.NoGraze();
            return coll;
        } else {
            collisionLocation = Vector2.zero;
            return CollisionMath.NoCollision;
        }
    }
    
    public void DoRegularUpdateCollision(bool collisionActive) {
        Pather.IsColliding = false;
        if (!collisionActive)
            goto finalize;
        Profiler.BeginSample("Pather collisions");
        int cut1 = (int)Math.Ceiling((cL - read_from + 1) * tailCutoffRatio);
        int cut2 = (int)Math.Ceiling((cL - read_from + 1) * headCutoffRatio);
        if (playerBullet.Try(out var plb)) {
            var collidees = ServiceLocator.FindAll<IPlayerPatherCollisionReceiver>();
            for (int ic = 0; ic < collidees.Count; ++ic) {
                if (collidees.GetIfExistsAt(ic, out var receiver) && receiver.Process(this, plb, cut1, cut2).collide) {
                    Pather.IsColliding = true;
                    Pather.myStyle.IterateCollideControls(Pather);
                }
            }
        } else {
            var collidees = ServiceLocator.FindAll<IEnemyPatherCollisionReceiver>();
            for (int ic = 0; ic < collidees.Count; ++ic) {
                if (collidees.GetIfExistsAt(ic, out var receiver) && receiver.Process(this, cut1, cut2).collide) {
                    Pather.IsColliding = true;
                    Pather.myStyle.IterateCollideControls(Pather);
                }
            }
        }
        Profiler.EndSample();
        finalize: ;
        Pather.FinalizeCollisionTimings();
    }
    
    public void FlipVelX() {
        movement.FlipX();
        intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
    }
    public void FlipVelY() {
        movement.FlipY();
        intersectStatus = SelfIntersectionStatus.CHECK_THIS_AND_NEXT;
    }

    public void SpawnSimple(string style) {
        for (int ii = texRptWidth; ii > read_from; ii -= FramePosCheck * 2) {
            BulletManager.RequestNullSimple(style, centers[ii], (centers[ii] - centers[ii-1]).normalized);
        }
    }
    
    
    public override void SetSprite(Sprite s, float yscale) {
        base.SetSprite(s, yscale);
        trailR.widthMultiplier = spriteBounds.y;
    }

    public override void Deactivate() {
        base.Deactivate();
        trailR.emitting = false;
        trailR.Clear();
    }

    public override void Activate() {
        UpdateGraphics();
        base.Activate();
        tr.localPosition = bpi.loc;
        trailR.SetPropertyBlock(pb);
        trailR.Clear();
        trailR.emitting = true;
    }


#if UNITY_EDITOR
    public void Draw() {
        Handles.color = Color.cyan;
        int cut1 = (int) Math.Ceiling((cL - read_from + 1) * tailCutoffRatio);
        int cut2 = Mathf.CeilToInt((cL - read_from + 1) * headCutoffRatio);
        GenericColliderInfo.DrawGizmosForSegments(centers, (read_from + cut1), 1, cL - cut2, Vector2.zero, scaledLineRadius, 0);
        /*
        Handles.color = Color.magenta;
        for (int ii = 0; ii < cL; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f * ii / cL);
        }
        Handles.color = Color.blue;
        for (int ii = 0; ii < cL; ++ii) {
            Handles.DrawWireDisc(vertsPtr[ii + cL].loc + (Vector3) velocity.rootPos, Vector3.forward, 0.005f + 0.005f *ii / cL);
        }*/
    }

    [ContextMenu("Debug info")]
    public void DebugPath() {
        //read_from = start + 1
        Logs.Log($"Start {read_from} Skip 1 End {centers.Length}", level: LogLevel.INFO);
    }
#endif
}
}