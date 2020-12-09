using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku.Descriptors;
using DMK.DataHoist;
using DMK.DMath;
using DMK.Expressions;
using DMK.Graphics;
using DMK.Pooling;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;

namespace DMK.Danmaku {

public partial class BulletManager {
    //Rendering variables
    //Note: while 1023 is the maximum shader array length, 511 is the maximum batch size for RyannPC support.
    private const int batchSize = 511; //duplicated in BulletIndirect array lens
    private MaterialPropertyBlock pb;
    private static readonly int posDirPropertyId = Shader.PropertyToID("posDirBuffer");
    private static readonly int tintPropertyId = Shader.PropertyToID("tintBuffer");
    private static readonly int timePropertyId = Shader.PropertyToID("timeBuffer");
    private static readonly int recolorBPropertyId = Shader.PropertyToID("recolorBBuffer");
    private static readonly int recolorWPropertyId = Shader.PropertyToID("recolorWBuffer");
    private static readonly Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private readonly Vector4[] posDirArr = new Vector4[batchSize];
    private readonly Vector4[] tintArr = new Vector4[batchSize];
    private readonly Vector4[] recolorBArr = new Vector4[batchSize];
    private readonly Vector4[] recolorWArr = new Vector4[batchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private readonly float[] timeArr = new float[batchSize];
    
    private const ushort CULL_EVERY_MASK = 127;
    /// <summary>
    /// SimpleBullet contains all information about a code-abstraction bullet except style information.
    /// As such, it can be freely shuttled between styles.
    /// </summary>
    public struct SimpleBullet {
        //96 byte struct. (92 unpacked)
            //BPY  = 8
            //TP   = 8
            //VS   = 32
            //V2   = 8
            //BPI  = 20
            //Flt  = 4
            //V2   = 8
            //Sx2  = 4
        [CanBeNull] public readonly BPY scaleFunc;
        [CanBeNull] public readonly SBV2 dirFunc;
        public Movement movement; //Don't make this readonly
        /// <summary>
        /// Accumulated position delta for each frame.
        /// Currently, this is only used for direction, and
        /// the delta is also put into BPI when this is generated.
        /// </summary>
        public Vector2 accDelta;
        public ParametricInfo bpi;
        public float scale;
        public Vector2 direction;
        
        public ushort grazeFrameCounter;
        public ushort cullFrameCounter;

        public SimpleBullet([CanBeNull] BPY scaleF, [CanBeNull] SBV2 dirF, Movement movement, int firingIndex, uint id, float timeOffset) {
            scaleFunc = scaleF;
            dirFunc = dirF;
            scale = 1f;
            this.movement = movement;
            grazeFrameCounter = cullFrameCounter = 0;
            this.accDelta = Vector2.zero;
            bpi = new ParametricInfo(movement.rootPos, firingIndex, id, timeOffset);
            direction = this.movement.UpdateZero(ref bpi, timeOffset);
            scale = scaleFunc?.Invoke(bpi) ?? 1f;
            if (dirFunc != null) direction = dirFunc(ref this);
        }
    }
    private readonly struct CollisionCheckResults {
        public readonly int damage;
        public readonly int graze;

        public CollisionCheckResults(int dmg, int graze) {
            this.damage = dmg;
            this.graze = graze;
        }
    }

    public abstract class AbsSimpleBulletCollection : CompactingArray<SimpleBullet> {
        public bool allowCameraCull = true;
        public abstract BehaviorEntity GetINodeAt(int sbcind, string behName, uint? bpiid, out uint sbid);
        public abstract void ClearControls();
        public abstract string Style { get; }

        /// <summary>
        /// Marks a bullet for deletion. You may continue operating on the bullet until the next Compact call, when
        /// it will actually be removed from memory.
        /// </summary>
        /// <param name="ind">Index of bullet.</param>
        /// <param name="destroy">Iff true, destroy hoisted data associated with the object.</param>
        public abstract void Delete(int ind, bool destroy);

        public abstract void Speedup(float ratio);
        public abstract float NextDT { get; }
        
#if UNITY_EDITOR
        public abstract int NumPcs { get; }
        public abstract object PcsAt(int ii);
#endif
    }
    //Instantiate this class directly for player bullets
    private class SimpleBulletCollection: AbsSimpleBulletCollection {
        public enum CollectionType {
            Normal,
            Softcull
        }

        public bool Active { get; private set; } = false;
        private readonly List<SimpleBulletCollection> targetList;

        public bool IsPlayer { get; private set; } = false;
        public void SetPlayer() {
            IsPlayer = true;
            bc.SetPlayer();
        }

        public void Activate() {
            if (!Active) {
                targetList.Add(this);
                //Log.Unity($"Activating pool {Style}", level: Log.Level.DEBUG1);
                Active = true;
            }
        }

        public void Deactivate() {
            Active = false;
            temp_last = 0;
        }

        public override string Style => bc.name;
        protected BulletInCode bc;

        public bool TryGetRecolor(out (TP4, TP4) recolor) => bc.recolor.Try(out recolor);

        [CanBeNull] public TP4 Tint => bc.Tint;

        public void SetCullRad(float r) => bc.CULL_RAD = r;

        public void SetRecolor(TP4 black, TP4 white) {
            if (!bc.Recolorizable) 
                throw new Exception($"Cannot set recolor on non-recolorizable pool {Style}");
            bc.recolor = (black, white);
        }

        public void SetTint(TP4 tint) {
            bc.Tint = tint;
        }
        
        //private readonly ResizableArray<BulletControl> pcs = new ResizableArray<BulletControl>(4);
        private readonly DMCompactingArray<BulletControl> pcs = new DMCompactingArray<BulletControl>(4);
#if UNITY_EDITOR
        public override int NumPcs => pcs.Count;
        public override object PcsAt(int ii) => pcs[ii];
#endif
        public int temp_last;
        private static readonly CollisionResult noColl = new CollisionResult(false, false);
        public SimpleBulletCollection(List<SimpleBulletCollection> target, BulletInCode bc) {
            this.bc = bc;
            this.targetList = target;
        }

        public SimpleBulletCollection CopySimplePool(List<SimpleBulletCollection> target, string newPool) => new SimpleBulletCollection(target, bc.Copy(newPool));
        public SimpleBulletCollection CopyPool(List<SimpleBulletCollection> target, string newPool) => GetCollectionForColliderType(target, bc.Copy(newPool));

        public MeshGenerator.RenderInfo GetOrLoadRI() => bc.GetOrLoadRI();

        public override BehaviorEntity GetINodeAt(int sbcind, string behName, uint? bpiid, out uint sbid) {
            ref SimpleBullet sb = ref arr[sbcind];
            sbid = sb.bpi.id;
            return BEHPooler.INode(sb.bpi.loc, V2RV2.Angle(sb.movement.angle), 
                sb.direction, sb.bpi.index, bpiid, behName);
        }

        public virtual CollectionType MetaType => CollectionType.Normal;

        public bool Deletable => bc.deletable;

        [UsedImplicitly]
        public virtual void AppendSoftcull(AbsSimpleBulletCollection sbc, int ii) {
            throw new NotImplementedException("Cannot softcull to non-cull style " + Style);
        }
        public static readonly ExFunction appendSoftcull = ExUtils.Wrap<SimpleBulletCollection>("AppendSoftcull", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});
        protected virtual CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) 
            => throw new NotImplementedException();

        public void ResetPoolMetadata() {
            bc.ResetMetadata();
            allowCameraCull = true;
        }

        public void AddPoolControl(BulletControl pc) => pcs.AddPriority(pc, pc.priority);
        
        public void PruneControls() {
            for (int ii = 0; ii < pcs.Count; ++ii) {
                if (pcs[ii].cT.Cancelled || !pcs[ii].persist(ParametricInfo.Zero)) {
                    pcs.Delete(ii);
                }
            }
            pcs.Compact();
        }
        public override void ClearControls() => pcs.Empty();

        public override void Delete(int ind, bool destroy) {
            if (destroy) DataHoisting.Destroy(arr[ind].bpi.id);
            base.Delete(ind);
        }

        public virtual void Add(ref SimpleBullet sb, bool isNew) {
            base.Add(ref sb);
            if (isNew) {
                int numPcs = pcs.Count;
                for (int pi = 0; pi < numPcs; ++pi) {
                    pcs[pi].action(this, count - 1, sb.bpi);
                }
            }
            if (rem[count - 1]) DeleteLast(); //Easy way to handle zero-frame deletion
        }
        public void AddFrom(AbsSimpleBulletCollection sbc, int ii) => Add(ref sbc[ii], false);
        public static readonly ExFunction addFrom = ExUtils.Wrap<SimpleBulletCollection>("AddFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});

        public void CopyNullFrom(AbsSimpleBulletCollection sbc, int ii) =>
            RequestNullSimple(Style, sbc[ii].bpi.loc, sbc[ii].direction);
        public static readonly ExFunction copyNullFrom = ExUtils.Wrap<SimpleBulletCollection>("CopyNullFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});

        public void CopyFrom(AbsSimpleBulletCollection sbc, int ii) => CopySimple(Style, sbc, ii);
        public static readonly ExFunction copyFrom = ExUtils.Wrap<SimpleBulletCollection>("CopyFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});
        
        public new void Add(ref SimpleBullet sb) => throw new Exception("Do not use SBC.Add");

        public override float NextDT => nextDT;
        private float nextDT;
        public override void Speedup(float ratio) => nextDT *= ratio;

        public virtual void UpdateVelocityAndControls() {
            int postVelPcs = pcs.FirstPriorityGT(BulletControl.POST_VEL_PRIORITY);
            int postDirPcs = pcs.FirstPriorityGT(BulletControl.POST_DIR_PRIORITY);
            int numPcs = pcs.Count;
            //Note on optimization: keeping accDelta in SB is faster(!) than either a local variable or a SBInProgress struct.
            for (int ii = 0; ii < temp_last; ++ii) {
                ref SimpleBullet sb = ref arr[ii];
                nextDT = ETime.FRAME_TIME;
                for (int pi = 0; pi < postVelPcs; ++pi) pcs[pi].action(this, ii, sb.bpi);
                sb.movement.UpdateDeltaAssignAcc(ref sb.bpi, out sb.accDelta, in nextDT);
                sb.scale = sb.scaleFunc?.Invoke(sb.bpi) ?? 1f;
                //See Bullet Notes > Colliding Pool Controls for details
                for (int pi = postVelPcs; pi < postDirPcs; ++pi) pcs[pi].action(this, ii, sb.bpi);
                if (sb.dirFunc != null) sb.direction = sb.dirFunc(ref sb);
                else {
                    float mag = sb.accDelta.x * sb.accDelta.x + sb.accDelta.y * sb.accDelta.y;
                    if (mag > M.MAG_ERR) {
                        mag = 1f / (float)Math.Sqrt(mag);
                        sb.direction.x = sb.accDelta.x * mag;
                        sb.direction.y = sb.accDelta.y * mag;
                    }
                }
                //Post-vel controls may destroy the bullet. As soon as this occurs, stop iterating
                for (int pi = postDirPcs; pi < numPcs && !rem[ii]; ++pi) pcs[pi].action(this, ii, sb.bpi);
            }
            PruneControls();
        }

        public virtual CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            int graze = 0;
            int collisionDamage = 0;
            for (int ii = 0; ii < count; ++ii) {
                // During velocity iteration, bullet controls may destroy some items, so we need to do null checks.
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref arr[ii];
                    bool checkGraze = false;
                    if (sbn.grazeFrameCounter-- == 0) {
                        sbn.grazeFrameCounter = 0;
                        checkGraze = true;
                    }
                    CollisionResult cr = CheckGrazeCollision(in hitbox, ref sbn);
                    if (cr.collide) {
                        collisionDamage = bc.damageAgainstPlayer;
                        if (bc.destructible) Delete(ii, true);
                    } else if (checkGraze && cr.graze) {
                        sbn.grazeFrameCounter = bc.grazeEveryFrames;
                        ++graze;
                    } else if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        Delete(ii, true);
                    }
                }
            }
            Compact();
            return new CollisionCheckResults(collisionDamage, graze);
        }

        public void NullCollisionCleanup() {
            Compact();
        }

        /// <summary>
        /// Player bullet X enemies update function.
        /// Note that all damage is recorded.
        /// Note that player bullets must be circular.
        /// </summary>
        public void CheckCollision(IReadOnlyList<Enemy.FrozenCollisionInfo> fci) {
            int fciL = fci.Count;
            for (int ii = 0; ii < count; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref arr[ii];
                    if ((++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        PlayerFireDataHoisting.Delete(sbn.bpi.id);
                        Delete(ii, true);
                    } else {
                        for (int ff = 0; ff < fciL; ++ff) {
                            if (fci[ff].Active && 
                                CollisionMath.CircleOnCircle(fci[ff].pos, fci[ff].radius, sbn.bpi.loc, bc.cc.effRadius)) {
                                if (bc.destructible || fci[ff].enemy.TryHitIndestructible(sbn.bpi.id, bc.againstEnemyCooldown)) {
                                    if (PlayerFireDataHoisting.Retrieve(sbn.bpi.id).Try(out var de)) {
                                        fci[ff].enemy.QueueDamage(de.bossDmg, de.stageDmg, PlayerTarget.location);
                                        fci[ff].enemy.ProcOnHit(de.eff, sbn.bpi.loc);
                                    }
                                    if (bc.destructible) {
                                        PlayerFireDataHoisting.Delete(sbn.bpi.id);
                                        Delete(ii, true);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Compact();
        }

        public void Reset() {
            // This should free links to BPY/VTP constructed by SMs going out of scope
            Empty(true);
            ResetPoolMetadata();
            temp_last = 0;
        }

        public void AssertControls(IReadOnlyList<BulletControl> new_controls) {
            int ci = 0;
            for (int pi = 0; pi < pcs.Count && ci < new_controls.Count; ++pi) {
                if (pcs[pi] == new_controls[ci]) ++ci;
            }
            //All controls matched
            if (ci == new_controls.Count) return;
            //No controls matched
            if (ci == 0) {
                for (int ii =0; ii < new_controls.Count; ++ii) AddPoolControl(new_controls[ii]);
                return;
            }
            //Some controls matched (?!)
            throw new Exception("AssertControls found that some, neither all nor none, of controls were matched.");
        }
        
    }

    /// <summary>
    /// This class is for bullets that have been soft-culled. It will not perform velocity or collision checks,
    /// and it ignores pool commands. It only updates bullet times and culls bullets after some time.
    /// </summary>
    private class DummySBC : SimpleBulletCollection {
        private readonly float ttl;
        private readonly float timeR;
        private readonly float rotR;
        public DummySBC(List<SimpleBulletCollection> target, BulletInCode bc, float ttl, float timeR, float rotR) : base(target, bc) {
            this.ttl = ttl;
            this.timeR = timeR;
            this.rotR = rotR / 2f;
        }
        public override CollectionType MetaType => CollectionType.Softcull;

        public override void Add(ref SimpleBullet sb, bool isNew) {
            sb.bpi.t = RNG.GetFloat(0, this.timeR);
            sb.direction = M.RotateVectorDeg(sb.direction, RNG.GetFloat(-rotR, rotR));
            base.Add(ref sb, isNew);
        }

        public override void AppendSoftcull(AbsSimpleBulletCollection sbc, int ii) {
            Add(ref sbc[ii], false);
            sbc.Delete(ii, true);
        }
        public override void UpdateVelocityAndControls() {
            for (int ii = 0; ii < temp_last; ++ii) {
                ref SimpleBullet sbn = ref arr[ii];
                sbn.bpi.t += ETime.FRAME_TIME;
                if (sbn.bpi.t > ttl) {
                    Delete(ii, false);
                }
            }
        }

        public override CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            Compact();
            return new CollisionCheckResults(0, 0);
        }
    }
    private class CircleSBC : SimpleBulletCollection {
        public CircleSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}

        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnCircle(in hitbox, sb.bpi.loc, bc.cc.radius * sb.scale);
    }
    private class RectSBC : SimpleBulletCollection {
        public RectSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRect(in hitbox, sb.bpi.loc, bc.cc.halfRect.x, 
                bc.cc.halfRect.y, bc.cc.maxDist2, sb.scale, sb.direction.x, sb.direction.y);
    }
    private class LineSBC : SimpleBulletCollection {
        public LineSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRotatedSegment(in hitbox, sb.bpi.loc, bc.cc.radius, bc.cc.linePt1, 
                bc.cc.delta, sb.scale, bc.cc.deltaMag2, bc.cc.maxDist2, sb.direction.x, sb.direction.y);
    }
    private class NoCollSBC : SimpleBulletCollection {
        public NoCollSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) 
            => CollisionResult.noColl;
    }

    private bool playerRendered;
    private bool enemyRendered;
    //Called via Camera.onPreCull event
    private void RenderBullets(Camera c) {
        if (!Application.isPlaying) { return; }
        if (playerRendered && enemyRendered) {
            playerRendered = enemyRendered = false;
        }
        RNG.RNG_ALLOWED = false;
        SimpleBulletCollection sbc;
        if ((c.cullingMask & ppLayerMask) != 0) {
            for (int ii = 0; ii < activePlayer.Count; ++ii) {
                sbc = activePlayer[ii];
                if (sbc.Count > 0) SwitchRenderPool(c, sbc, ppRenderLayer);
            }
            playerRendered = true;
        }
        if ((c.cullingMask & epLayerMask) != 0) {
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                sbc = activeNpc[ii];
                if (sbc.Count > 0) SwitchRenderPool(c, sbc, epRenderLayer);
            }
            //customEmptyStyle bullets do not need to be rendered
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                sbc = activeCNpc[ii];
                if (sbc.Count > 0) SwitchRenderPool(c, sbc, epRenderLayer);
            }
            enemyRendered = true;
        }
        RNG.RNG_ALLOWED = true;
    }

    private void SwitchRenderPool(Camera c, SimpleBulletCollection pool, int layer) {
        if (SaveData.s.LegacyRenderer) LegacyRenderPool(c, pool, layer);
        else RenderPool(c, pool, layer);
    }
    private void LegacyRenderPool(Camera c, SimpleBulletCollection pool, int layer) {
        if (pool.TryGetRecolor(out var rc)) {
            LegacyRenderPool_Recolorizable(c, pool, layer, rc);
            return;
        }
        var tint = pool.Tint;
        var hasTint = tint != null;
        int ii = 0;
        MeshGenerator.RenderInfo ri = pool.GetOrLoadRI();
        for (int ct = pool.Count; ct > 0; ct -= batchSize) {
            int run = Math.Min(ct, batchSize);
            for (int ib = 0; ib < run; ++ib, ++ii) {
                ref SimpleBullet sb = ref pool.arr[ii];
                ref var m = ref matArr[ib];
                m.m00 = m.m11 = sb.direction.x * sb.scale;
                m.m10 = sb.direction.y * sb.scale;
                m.m01 = -m.m10;
                m.m22 = m.m33 = 1;
                m.m03 = sb.bpi.loc.x;
                m.m13 = sb.bpi.loc.y;
                if (hasTint) tintArr[ib] = tint(sb.bpi);
                timeArr[ib] = sb.bpi.t;
            }
            if (hasTint) pb.SetVectorArray(tintPropertyId, tintArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            CallLegacyRender(ri, c, layer, run);
        }
    }
    
    private void LegacyRenderPool_Recolorizable(Camera c, SimpleBulletCollection pool, int layer, 
        (TP4 black, TP4 white) rc) {
        var tint = pool.Tint;
        var hasTint = tint != null;
        int ii = 0;
        MeshGenerator.RenderInfo ri = pool.GetOrLoadRI();
        for (int ct = pool.Count; ct > 0; ct -= batchSize) {
            int run = Math.Min(ct, batchSize);
            for (int ib = 0; ib < run; ++ib, ++ii) {
                ref SimpleBullet sb = ref pool.arr[ii];
                ref var m = ref matArr[ib];
                m.m00 = m.m11 = sb.direction.x * sb.scale;
                m.m10 = sb.direction.y * sb.scale;
                m.m01 = -m.m10;
                m.m22 = m.m33 = 1;
                m.m03 = sb.bpi.loc.x;
                m.m13 = sb.bpi.loc.y;
                recolorBArr[ib] = rc.black(sb.bpi);
                recolorWArr[ib] = rc.white(sb.bpi);
                if (hasTint) tintArr[ib] = tint(sb.bpi);
                timeArr[ib] = sb.bpi.t;
            }
            pb.SetVectorArray(recolorBPropertyId, recolorBArr);
            pb.SetVectorArray(recolorWPropertyId, recolorWArr);
            if (hasTint) pb.SetVectorArray(tintPropertyId, tintArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            CallLegacyRender(ri, c, layer, run);
        }
    }

    private static readonly Vector4 white = Vector4.one;
    private void RenderPool(Camera c, SimpleBulletCollection pool, int layer) {
        if (pool.TryGetRecolor(out var rc)) {
            RenderPool_Recolorizable(c, pool, layer, rc);
            return;
        }
        var tint = pool.Tint;
        var hasTint = tint != null;
        int ii = 0;
        MeshGenerator.RenderInfo ri = pool.GetOrLoadRI();
        for (int ct = pool.Count; ct > 0; ct -= batchSize) {
            int run = Math.Min(ct, batchSize);
            for (int ib = 0; ib < run; ++ib, ++ii) {
                ref SimpleBullet sb = ref pool.arr[ii];
                posDirArr[ib].x = sb.bpi.loc.x;
                posDirArr[ib].y = sb.bpi.loc.y;
                posDirArr[ib].z = sb.direction.x * sb.scale;
                posDirArr[ib].w = sb.direction.y * sb.scale; 
                timeArr[ib] = sb.bpi.t;
                if (hasTint) tintArr[ib] = tint(sb.bpi);
            }
            pb.SetVectorArray(posDirPropertyId, posDirArr);
            if (hasTint) pb.SetVectorArray(tintPropertyId, tintArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            CallRender(ri, c, layer, run);
        }
    }

    private void RenderPool_Recolorizable(Camera c, SimpleBulletCollection pool, int layer, (TP4 black, TP4 white) rc) {
        var tint = pool.Tint;
        var hasTint = tint != null;
        int ii = 0;
        MeshGenerator.RenderInfo ri = pool.GetOrLoadRI();
        for (int ct = pool.Count; ct > 0; ct -= batchSize) {
            int run = Math.Min(ct, batchSize);
            for (int ib = 0; ib < run; ++ib, ++ii) {
                ref SimpleBullet sb = ref pool.arr[ii];
                posDirArr[ib].x = sb.bpi.loc.x;
                posDirArr[ib].y = sb.bpi.loc.y;
                posDirArr[ib].z = sb.direction.x * sb.scale;
                posDirArr[ib].w = sb.direction.y * sb.scale;
                recolorBArr[ib] = rc.black(sb.bpi);
                recolorWArr[ib] = rc.white(sb.bpi);
                timeArr[ib] = sb.bpi.t;
                if (hasTint) tintArr[ib] = tint(sb.bpi);
            }
            pb.SetVectorArray(recolorBPropertyId, recolorBArr);
            pb.SetVectorArray(recolorWPropertyId, recolorWArr);
            pb.SetVectorArray(posDirPropertyId, posDirArr);
            if (hasTint) pb.SetVectorArray(tintPropertyId, tintArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            CallRender(ri, c, layer, run);
        }
    }

    private void CallLegacyRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        UnityEngine.Graphics.DrawMeshInstanced(ri.mesh, 0, ri.material,
            matArr,
            count: ct,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layer,
            camera: c);
    }
    private void CallRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        UnityEngine.Graphics.DrawMeshInstancedProcedural(ri.mesh, 0, ri.material,
          bounds: drawBounds,
          count: ct,
          properties: pb,
          castShadows: ShadowCastingMode.Off,
          receiveShadows: false,
          layer: layer,
          camera: c);
    }

    private unsafe void PrepareRendering() {
        Debug.Log($"Sizes: BS {Marshal.SizeOf(typeof(SimpleBullet))}, VelStruct {Marshal.SizeOf(typeof(Movement))}, " +
                  $"BPI {Marshal.SizeOf(typeof(ParametricInfo))}, CollInfo {Marshal.SizeOf(typeof(CollisionResult))}, " +
                  $"Float {sizeof(float)} (4), Long {sizeof(long)} (8), V2 {sizeof(Vector2)} (8)");
    }

#if UNITY_EDITOR
    [ContextMenu("Debug bullet numbers")]
    public void DebugBulletNums() {
        int total = 0;
        foreach (var pool in simpleBulletPools.Values) {
            total += pool.Count;
            if (pool.Count > 0) Log.Unity($"{pool.Style}: {pool.Count}", level: Log.Level.INFO);
            if (pool.NumPcs > 0) Log.Unity($"{pool.Style} has {pool.NumPcs} controls", level: Log.Level.INFO);
        }
        total += Bullet.NumBullets;
        Log.Unity($"Custom pools: {string.Join(", ", activeCNpc.Select(x => x.Style))}");
        Log.Unity($"Custom empty pools: {string.Join(", ", activeCEmpty.Select(x => x.Style))}");
        Log.Unity($"Fancy bullets: {Bullet.NumBullets}", level: Log.Level.INFO);
        Log.Unity($"Total: {total}", level: Log.Level.INFO);
    }
    #endif
}

}