using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku.Descriptors;
using DMK.DataHoist;
using DMK.DMath;
using DMK.Expressions;
using DMK.Graphics;
using DMK.Pooling;
using DMK.SM;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace DMK.Danmaku {

public partial class BulletManager {
    //Rendering variables
    //Note: while 1023 is the maximum shader array length, 511 is the maximum batch size for RyannPC support.
    private const int batchSize = 511; //duplicated in BulletIndirect array lens
    private MaterialPropertyBlock pb = null!;
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
        public readonly BPY? scaleFunc;
        public readonly SBV2? dirFunc;
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

        public SimpleBullet(BPY? scaleF, SBV2? dirF, in Movement movement, ParametricInfo bpi) {
            scaleFunc = scaleF;
            dirFunc = dirF;
            scale = 1f;
            this.movement = movement;
            grazeFrameCounter = cullFrameCounter = 0;
            this.accDelta = Vector2.zero;
            this.bpi = bpi;
            direction = this.movement.UpdateZero(ref this.bpi);
            scale = scaleFunc?.Invoke(this.bpi) ?? 1f;
            if (dirFunc != null) direction = dirFunc(ref this);
        }

        public SimpleBullet(ref SimpleBullet sb, uint? newId = null) {
            scaleFunc = sb.scaleFunc;
            dirFunc = sb.dirFunc;
            scale = sb.scale;
            movement = sb.movement;
            grazeFrameCounter = cullFrameCounter = 0;
            accDelta = sb.accDelta;
            bpi = sb.bpi.CopyCtx(newId ?? sb.bpi.id);
            direction = sb.direction;
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
        public enum CollectionType {
            Normal,
            Softcull
        }
        public virtual CollectionType MetaType => CollectionType.Normal;
        
        public bool allowCameraCull = true;
        public abstract BehaviorEntity GetINodeAt(int sbcind, string behName);
        public abstract string Style { get; }
        
        protected readonly DMCompactingArray<BulletControl> controls = new DMCompactingArray<BulletControl>(4);

        protected AbsSimpleBulletCollection() : base(1, 128) { }
        
        public void AddPoolControl(BulletControl pc) => controls.AddPriority(pc, pc.priority);
        
        public void PruneControls() {
            for (int ii = 0; ii < controls.Count; ++ii) {
                if (controls[ii].cT.Cancelled || !controls[ii].persist(ParametricInfo.Zero)) {
                    controls.Delete(ii);
                }
            }
            controls.Compact();
        }
        public void ClearControls() => controls.Empty();
		
        public void AssertControls(IReadOnlyList<BulletControl> new_controls) {
            int ci = 0;
            for (int pi = 0; pi < controls.Count && ci < new_controls.Count; ++pi) {
                if (controls[pi] == new_controls[ci]) ++ci;
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

        public abstract void SetCullRad(float r);
        public abstract void SetDeleteActive(bool active);

        public abstract void SetRecolor(TP4 black, TP4 white);

        public abstract void SetTint(TP4 tint);

        //TODO: investigate if isNew is actually required; is it possible to always apply the initial controls?
        public abstract void Add(ref SimpleBullet sb, bool isNew);
        
        [UsedImplicitly]
        public void TransferFrom(AbsSimpleBulletCollection sbc, int ii) {
            var sb = new SimpleBullet(ref sbc[ii]);
            Add(ref sb, false);
            sbc.DeleteSB(ii);
        }

        public static readonly ExFunction transferFrom = ExUtils.Wrap<AbsSimpleBulletCollection>("TransferFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});

        [UsedImplicitly]
        public void CopyNullFrom(AbsSimpleBulletCollection sbc, int ii) =>
            RequestNullSimple(Style, sbc[ii].bpi.loc, sbc[ii].direction);
        public static readonly ExFunction copyNullFrom = ExUtils.Wrap<AbsSimpleBulletCollection>("CopyNullFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});

        public void CopyNullWithSoftcullDelay(in SoftcullProperties props, AbsSimpleBulletCollection sbc, int ii) {
            RequestNullSimple(Style, sbc[ii].bpi.loc, sbc[ii].direction, props.AdvanceTime(sbc[ii].bpi.loc));
        }

        [UsedImplicitly]
        public void CopyFrom(AbsSimpleBulletCollection sbc, int ii) {
            var sb = new SimpleBullet(ref sbc[ii], RNG.GetUInt());
            Add(ref sb, false);
        }

        public static readonly ExFunction copyFrom = ExUtils.Wrap<AbsSimpleBulletCollection>("CopyFrom", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});

        /// <summary>
        /// Marks a bullet for deletion. You may continue operating on the bullet until the next Compact call, when
        /// it will actually be removed from memory, but the FiringCtx will be invalid immediately.
        /// </summary>
        /// <param name="ind">Index of bullet.</param>
        public void DeleteSB(int ind) {
            if (!rem[ind]) {
                arr[ind].bpi.Dispose();
                Delete(ind);
            }
        }

        public abstract void Speedup(float ratio);
        public abstract float NextDT { get; }
        
        
        
        
#if UNITY_EDITOR
        public abstract int NumControls { get; }
        public abstract object ControlAt(int ii);
#endif
    }
    //Instantiate this class directly for player bullets
    private class SimpleBulletCollection: AbsSimpleBulletCollection {

        public bool Active { get; private set; } = false;
        private readonly List<SimpleBulletCollection> targetList;

        public bool IsPlayer { get; private set; } = false;

        public override string Style => bc.name;
        protected BulletInCode bc;
        public bool Deletable => bc.deletable;
        public int temp_last;
        private static readonly CollisionResult noColl = new CollisionResult(false, false);

        public TP4? Tint => bc.Tint;
        public void SetPlayer() {
            IsPlayer = true;
            bc.SetPlayer();
        }

        public bool TryGetRecolor(out (TP4, TP4) recolor) => bc.recolor.Try(out recolor);

        public override void SetCullRad(float r) => bc.CULL_RAD = r;
        public override void SetDeleteActive(bool active) => bc.deletable = active;

        public override void SetRecolor(TP4 black, TP4 white) {
            if (!bc.Recolorizable) 
                throw new Exception($"Cannot set recolor on non-recolorizable pool {Style}");
            bc.recolor = (black, white);
        }

        public override void SetTint(TP4 tint) {
            bc.Tint = tint;
        }
        

        public SimpleBulletCollection(List<SimpleBulletCollection> target, BulletInCode bc) {
            this.bc = bc;
            this.targetList = target;
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

        public SimpleBulletCollection CopySimplePool(List<SimpleBulletCollection> target, string newPool) => new SimpleBulletCollection(target, bc.Copy(newPool));
        public SimpleBulletCollection CopyPool(List<SimpleBulletCollection> target, string newPool) => GetCollectionForColliderType(target, bc.Copy(newPool));

        public MeshGenerator.RenderInfo GetOrLoadRI() => bc.GetOrLoadRI();

        public override BehaviorEntity GetINodeAt(int sbcind, string behName) {
            ref SimpleBullet sb = ref arr[sbcind];
            var mov = new Movement(sb.bpi.loc, V2RV2.Angle(sb.movement.angle));
            return BEHPooler.INode(mov, new ParametricInfo(in mov, sb.bpi.index), sb.direction, behName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) 
            => throw new NotImplementedException();

        public void ResetPoolMetadata() {
            bc.ResetMetadata();
            allowCameraCull = true;
        }

        public override void Add(ref SimpleBullet sb, bool isNew) {
            base.Add(ref sb);
            if (isNew) {
                int numPcs = controls.Count;
                var ind = count - 1; //count may change if a deletion/addition occurs
                for (int pi = 0; pi < numPcs && !rem[ind]; ++pi) {
                    controls[pi].action(this, ind, sb.bpi);
                }
            }
        }
        
        public new void Add(ref SimpleBullet sb) => throw new Exception("Do not use SBC.Add");

        public override float NextDT => nextDT;
        private float nextDT;
        public override void Speedup(float ratio) => nextDT *= ratio;

        public virtual void UpdateVelocityAndControls() {
            int postVelPcs = controls.FirstPriorityGT(BulletControl.POST_VEL_PRIORITY);
            int postDirPcs = controls.FirstPriorityGT(BulletControl.POST_DIR_PRIORITY);
            int numPcs = controls.Count;
            //Note on optimization: keeping accDelta in SB is faster(!) than either a local variable or a SBInProgress struct.
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sb = ref arr[ii];
                    nextDT = ETime.FRAME_TIME;
                    
                    for (int pi = 0; pi < postVelPcs; ++pi) 
                        controls[pi].action(this, ii, sb.bpi);
                    
                    //in nextDT is a significant optimization
                    sb.movement.UpdateDeltaAssignAcc(ref sb.bpi, out sb.accDelta, in nextDT);
                    if (sb.scaleFunc != null)
                        sb.scale = sb.scaleFunc(sb.bpi);
                    
                    //See Bullet Notes > Colliding Pool Controls for details
                    for (int pi = postVelPcs; pi < postDirPcs; ++pi) 
                        controls[pi].action(this, ii, sb.bpi);
                    
                    if (sb.dirFunc == null) {
                        float mag = sb.accDelta.x * sb.accDelta.x + sb.accDelta.y * sb.accDelta.y;
                        if (mag > M.MAG_ERR) {
                            mag = 1f / (float) Math.Sqrt(mag);
                            sb.direction.x = sb.accDelta.x * mag;
                            sb.direction.y = sb.accDelta.y * mag;
                        }
                    } else
                        sb.direction = sb.dirFunc(ref sb);
                    
                    //Post-vel controls may destroy the bullet. As soon as this occurs, stop iterating
                    for (int pi = postDirPcs; pi < numPcs && !rem[ii]; ++pi) 
                        controls[pi].action(this, ii, sb.bpi);
                }
            }
            PruneControls();
        }

        public virtual CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            int graze = 0;
            int collisionDamage = 0;
            Profiler.BeginSample("CheckCollision");
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
                        if (bc.destructible) DeleteSB(ii);
                    } else if (checkGraze && cr.graze) {
                        sbn.grazeFrameCounter = bc.grazeEveryFrames;
                        ++graze;
                    } else if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        DeleteSB(ii);
                    }
                }
            }
            Profiler.EndSample();
            Profiler.BeginSample("Compact");
            if (NullElements > Math.Min(1000, Count / 10))
                Compact();
            Profiler.EndSample();
            return new CollisionCheckResults(collisionDamage, graze);
        }

        public void NullCollisionCleanup() {
            for (int ii = 0; ii < count; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref arr[ii];
                    if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        DeleteSB(ii);
                    }
                }
            }
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
                        DeleteSB(ii);
                    } else {
                        for (int ff = 0; ff < fciL; ++ff) {
                            if (fci[ff].Active && 
                                CollisionMath.CircleOnCircle(fci[ff].pos, fci[ff].radius, sbn.bpi.loc, bc.cc.effRadius)) {
                                if (bc.destructible || fci[ff].enemy.TryHitIndestructible(sbn.bpi.id, bc.againstEnemyCooldown)) {
                                    if (sbn.bpi.ctx.playerFireCfg.Try(out var de)) {
                                        fci[ff].enemy.QueuePlayerDamage(de.bossDmg, de.stageDmg, PlayerTarget.location);
                                        fci[ff].enemy.ProcOnHit(de.eff, sbn.bpi.loc);
                                    }
                                    if (bc.destructible) {
                                        DeleteSB(ii);
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
            Empty();
            ResetPoolMetadata();
            temp_last = 0;
        }

        
        private void LegacyRender(Camera c, BulletManager bm, int layer) {
            if (TryGetRecolor(out var rc)) {
                LegacyRenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            for (int ii = 0; ii < Count;) {
                int ib = 0;
                for (; ib < batchSize && ii < Count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref arr[ii];
                        ref var m = ref bm.matArr[ib];
                        m.m00 = m.m11 = sb.direction.x * sb.scale;
                        m.m10 = sb.direction.y * sb.scale;
                        m.m01 = -m.m10;
                        m.m22 = m.m33 = 1;
                        m.m03 = sb.bpi.loc.x;
                        m.m13 = sb.bpi.loc.y;
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        bm.timeArr[ib] = sb.bpi.t;
                        ++ib;
                    }
                }
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArr);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallLegacyRender(ri, c, layer, ib);
            }
        }
        
        private void LegacyRenderRecolorizable(Camera c, BulletManager bm, int layer, 
            (TP4 black, TP4 white) rc) {
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            for (int ii = 0; ii < Count;) {
                int ib = 0;
                for (; ib < batchSize && ii < Count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref arr[ii];
                        ref var m = ref bm.matArr[ib];
                        m.m00 = m.m11 = sb.direction.x * sb.scale;
                        m.m10 = sb.direction.y * sb.scale;
                        m.m01 = -m.m10;
                        m.m22 = m.m33 = 1;
                        m.m03 = sb.bpi.loc.x;
                        m.m13 = sb.bpi.loc.y;
                        bm.recolorBArr[ib] = rc.black(sb.bpi);
                        bm.recolorWArr[ib] = rc.white(sb.bpi);
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        bm.timeArr[ib] = sb.bpi.t;
                        ++ib;
                    }
                }
                bm.pb.SetVectorArray(recolorBPropertyId, bm.recolorBArr);
                bm.pb.SetVectorArray(recolorWPropertyId, bm.recolorWArr);
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArr);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallLegacyRender(ri, c, layer, ib);
            }
        }

        private void Render(Camera c, BulletManager bm, int layer) {
            if (TryGetRecolor(out var rc)) {
                RenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            for (int ii = 0; ii < Count;) {
                int ib = 0;
                for (; ib < batchSize && ii < Count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref arr[ii];
                        bm.posDirArr[ib].x = sb.bpi.loc.x;
                        bm.posDirArr[ib].y = sb.bpi.loc.y;
                        bm.posDirArr[ib].z = sb.direction.x * sb.scale;
                        bm.posDirArr[ib].w = sb.direction.y * sb.scale; 
                        bm.timeArr[ib] = sb.bpi.t;
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        ++ib;
                    }
                }
                bm.pb.SetVectorArray(posDirPropertyId, bm.posDirArr);
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArr);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallRender(ri, c, layer, ib);
            }
        }

        private void RenderRecolorizable(Camera c, BulletManager bm, int layer, (TP4 black, TP4 white) rc) {
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            
            for (int ii = 0; ii < Count;) {
                int ib = 0;
                for (; ib < batchSize && ii < Count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref arr[ii];
                        bm.posDirArr[ib].x = sb.bpi.loc.x;
                        bm.posDirArr[ib].y = sb.bpi.loc.y;
                        bm.posDirArr[ib].z = sb.direction.x * sb.scale;
                        bm.posDirArr[ib].w = sb.direction.y * sb.scale; 
                        bm.recolorBArr[ib] = rc.black(sb.bpi);
                        bm.recolorWArr[ib] = rc.white(sb.bpi);
                        bm.timeArr[ib] = sb.bpi.t;
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        ++ib;
                    }
                }
                bm.pb.SetVectorArray(recolorBPropertyId, bm.recolorBArr);
                bm.pb.SetVectorArray(recolorWPropertyId, bm.recolorWArr);
                bm.pb.SetVectorArray(posDirPropertyId, bm.posDirArr);
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArr);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallRender(ri, c, layer, ib);
            }
        }

        public void SwitchRender(Camera c, BulletManager bm, int layer) {
            if (SaveData.s.LegacyRenderer) LegacyRender(c, bm, layer);
            else Render(c, bm, layer);
        }
        
        
#if UNITY_EDITOR
        public override int NumControls => controls.Count;
        public override object ControlAt(int ii) => controls[ii];
#endif
        
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
            sb.bpi.t += RNG.GetFloat(0, this.timeR);
            sb.direction = M.RotateVectorDeg(sb.direction, RNG.GetFloat(-rotR, rotR));
            base.Add(ref sb, isNew);
        }

        public override void UpdateVelocityAndControls() {
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref arr[ii];
                    sbn.bpi.t += ETime.FRAME_TIME;
                    if (sbn.bpi.t > ttl) {
                        DeleteSB(ii);
                    }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnCircle(in hitbox, sb.bpi.loc, bc.cc.radius * sb.scale);
    }
    private class RectSBC : SimpleBulletCollection {
        public RectSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRect(in hitbox, sb.bpi.loc, bc.cc.halfRect.x, 
                bc.cc.halfRect.y, bc.cc.maxDist2, sb.scale, sb.direction.x, sb.direction.y);
    }
    private class LineSBC : SimpleBulletCollection {
        public LineSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRotatedSegment(in hitbox, sb.bpi.loc, bc.cc.radius, bc.cc.linePt1, 
                bc.cc.delta, sb.scale, bc.cc.deltaMag2, bc.cc.maxDist2, sb.direction.x, sb.direction.y);
    }
    private class NoCollSBC : SimpleBulletCollection {
        public NoCollSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (sbc.Count > 0) sbc.SwitchRender(c, this, ppRenderLayer);
            }
            playerRendered = true;
        }
        if ((c.cullingMask & epLayerMask) != 0) {
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                sbc = activeNpc[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, epRenderLayer);
            }
            //customEmptyStyle bullets do not need to be rendered
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                sbc = activeCNpc[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, epRenderLayer);
            }
            enemyRendered = true;
        }
        RNG.RNG_ALLOWED = true;
    }

    private void CallLegacyRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        if (ct == 0) return;
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
        if (ct == 0) return;
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
#if UNITY_EDITOR
        Debug.Log($"Sizes: BS {Marshal.SizeOf(typeof(SimpleBullet))},  Movement {Marshal.SizeOf(typeof(Movement))}, " +
                  $"BPI {Marshal.SizeOf(typeof(ParametricInfo))}, CollInfo {Marshal.SizeOf(typeof(CollisionResult))}, " +
                  $"Float {sizeof(float)} (4), Long {sizeof(long)} (8), V2 {sizeof(Vector2)} (8)");
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Debug FCTX usage")]
    public void DebugFCTX() {
        Log.Unity($"Alloc {FiringCtx.Allocated} / Popped {FiringCtx.Popped} / Cached {FiringCtx.Recached} / Copied {FiringCtx.Copied}");
    }
    [ContextMenu("Debug bullet numbers")]
    public void DebugBulletNums() {
        int total = 0;
        foreach (var pool in simpleBulletPools.Values) {
            total += pool.Count;
            if (pool.Count > 0) Log.Unity($"{pool.Style}: {pool.Count}", level: Log.Level.INFO);
            if (pool.NumControls > 0) Log.Unity($"{pool.Style} has {pool.NumControls} controls", level: Log.Level.INFO);
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