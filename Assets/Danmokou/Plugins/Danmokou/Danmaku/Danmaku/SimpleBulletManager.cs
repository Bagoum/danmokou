using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Danmokou.Danmaku {

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
    private static readonly Bounds drawBounds = new(Vector3.zero, Vector3.one * 1000f);
    private readonly Vector4[] posDirArr = new Vector4[batchSize];
    private readonly Vector4[] tintArr = new Vector4[batchSize];
    private readonly Vector4[] recolorBArr = new Vector4[batchSize];
    private readonly Vector4[] recolorWArr = new Vector4[batchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private readonly float[] timeArr = new float[batchSize];
    
    private const ushort CULL_EVERY_MASK = 127;
    /// <summary>
    /// A container for all information about a code-abstraction bullet except style information.
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

        /// <summary>
        /// Constructor for copying a SimpleBullet.
        /// </summary>
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
    public readonly struct CollisionCheckResults {
        public readonly int damage;
        public readonly int graze;

        public CollisionCheckResults(int dmg, int graze) {
            this.damage = dmg;
            this.graze = graze;
        }
    }

    //Instantiate this class directly for player bullets
    public class SimpleBulletCollection: CompactingArray<SimpleBullet> {
        public enum CollectionType {
            /// <summary>
            /// Empty bullets (no display or collision, used for guiding; player variants and copies included)
            /// </summary>
            Empty,
            Normal,
            /// <summary>
            /// Bullets such as cwheel representing animations played when a normal bullet is destroyed
            /// </summary>
            Softcull,
            /// <summary>
            /// Afterimages resulting when a normal bullet is destroyed
            /// </summary>
            Culled
        }
        private static readonly CollisionResult noColl = new(false, false);

        public bool Active { get; private set; } = false;
        public bool IsPlayer { get; private set; } = false;
        public BulletInCode BC { get; }
        protected virtual SimpleBulletFader Fade => BC.FadeIn;
        private readonly List<SimpleBulletCollection> targetList;
        public int temp_last;
        /// <summary>
        /// Copied pools have this set
        /// </summary>
        private SimpleBulletCollection? original;
        private CulledBulletCollection? culled;
        protected readonly DMCompactingArray<BulletControl> controls = new(4);
        private readonly DMCompactingArray<BulletControl> onCollideControls = new(2);
        
        public virtual CollectionType MetaType => CollectionType.Normal;
        public string Style => BC.name;
        public TP4? Tint => BC.Tint.Value;
        public bool SubjectToAutocull =>
            !IsPlayer && MetaType == CollectionType.Normal;
        public bool IsCopy => original != null;
        public CulledBulletCollection Culled => original?.Culled ?? (culled ??= new CulledBulletCollection(this));
        
        public SimpleBulletCollection(List<SimpleBulletCollection> target, BulletInCode bc) : base(1, 128) {
            this.BC = bc;
            this.targetList = target;
        }

        public void SetOriginal(SimpleBulletCollection orig) {
            original = orig;
        }
        public void SetPlayer() {
            //bc.SetPlayer should not be called twice
            if (!IsPlayer) {
                IsPlayer = true;
                BC.SetPlayer();
            }
        }

        public IDisposable SetRecolor(TP4 black, TP4 white) {
            if (!BC.Recolorizable) 
                throw new Exception($"Cannot set recolor on non-recolorizable pool {Style}");
            return BC.Recolor.AddConst((black, white));
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
        
        #region Copiers

        public BulletInCode CopyBC(string newPool) => BC.Copy(newPool);
        public SimpleBulletCollection CopySimplePool(List<SimpleBulletCollection> target, string newPool) => new(target, BC.Copy(newPool));
        public SimpleBulletCollection CopyPool(List<SimpleBulletCollection> target, string newPool) => GetCollectionForColliderType(target, BC.Copy(newPool));
        
        #endregion

        public MeshGenerator.RenderInfo GetOrLoadRI() => BC.GetOrLoadRI();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) 
            => throw new NotImplementedException();

        #region Operators
        
        //TODO: investigate if isNew is actually required; is it possible to always apply the initial controls?
        public virtual void Add(ref SimpleBullet sb, bool isNew) {
            base.AddRef(ref sb);
            if (isNew) {
                int numPcs = controls.Count;
                var ind = count - 1; //count may change if a deletion/addition occurs
                for (int pi = 0; pi < numPcs && !rem[ind]; ++pi) {
                    controls[pi].action(this, ind, sb.bpi, controls[pi].cT);
                }
            }
        }
        
        public new void AddRef(ref SimpleBullet sb) => throw new Exception("Do not use SBC.Add");
        public new void Add(SimpleBullet sb) => throw new Exception("Do not use SBC.Add");
        
        public virtual void MakeCulledCopy(int ii) {
            Culled.AddCulled(ref Data[ii]);
        }
        
        public void AddBulletControl(BulletControl pc) {
            if (pc.priority == BulletControl.P_ON_COLLIDE)
                onCollideControls.Add(pc);
            else
                controls.AddPriority(pc, pc.priority);
        }

        public BehaviorEntity GetINodeAt(int sbcind, string behName) {
            ref SimpleBullet sb = ref Data[sbcind];
            var mov = new Movement(sb.bpi.loc, V2RV2.Angle(sb.movement.angle));
            return BEHPooler.INode(mov, new ParametricInfo(in mov, sb.bpi.index), sb.direction, behName);
        }
        
        [UsedImplicitly]
        public BehaviorEntity RunINodeAt(int ii, StateMachine target, ICancellee cT) {
            var bpi = Data[ii].bpi;
            var inode = GetINodeAt(ii, "bulletcontrol-sm-triggered");
            //Note: this pattern is safe because GCX is copied immediately by SMRunner
            using var gcx = bpi.ctx.RevertToGCX(inode);
            gcx.fs["bulletTime"] = bpi.t;
            _ = inode.RunExternalSM(SMRunner.Cull(target, cT, gcx));
            return inode;
        }
        
        [UsedImplicitly]
        public void TransferFrom(SimpleBulletCollection sbc, int ii) {
            var sb = new SimpleBullet(ref sbc[ii]);
            Add(ref sb, false);
            sbc.DeleteSB(ii);
        }
        
        [UsedImplicitly]
        public void CopyNullFrom(SimpleBulletCollection sbc, int ii, SoftcullProperties? advancer) =>
            RequestNullSimple(Style, sbc[ii].bpi.loc, sbc[ii].direction, advancer?.AdvanceTime(sbc[ii].bpi.loc) ?? 0f);

        [UsedImplicitly]
        public void CopyFrom(SimpleBulletCollection sbc, int ii) {
            var sb = new SimpleBullet(ref sbc[ii], RNG.GetUInt());
            Add(ref sb, false);
        }
        
        public void Softcull(SimpleBulletCollection? target, int ii, SoftcullProperties? advancer) {
            MakeCulledCopy(ii);
            target?.CopyNullFrom(this, ii, advancer);
            DeleteSB(ii);
        }
        
        /// <summary>
        /// Marks a bullet for deletion. You may continue operating on the bullet until the next Compact call, when
        /// it will actually be removed from memory, but the FiringCtx will be invalid immediately.
        /// </summary>
        /// <param name="ind">Index of bullet.</param>
        public void DeleteSB(int ind) {
            if (!rem[ind]) {
                Data[ind].bpi.Dispose();
                Delete(ind);
            }
        }

        /// <summary>
        /// Same as DeleteSB, but also runs onCollideControls.
        /// </summary>
        /// <param name="ind"></param>
        public void DeleteSB_Collision(int ind) {
            if (!rem[ind]) {
                for (int ii = 0; ii < onCollideControls.Count; ++ii) {
                    onCollideControls[ii].action(this, ind, Data[ind].bpi, onCollideControls[ii].cT);
                }
                DeleteSB(ind);
            }
        }
        #endregion

        #region Updaters
        public float NextDT => nextDT;
        private float nextDT;
        public void Speedup(float ratio) => nextDT *= ratio;
        
        [UsedImplicitly] //batch command uses this to stop when a bullet is destroyed
        public bool IsAlive(int ind) => !rem[ind];

        public virtual void UpdateVelocityAndControls() {
            PruneControlsCancellation();
            int postVelPcs = controls.FirstPriorityGT(BulletControl.POST_VEL_PRIORITY);
            int postDirPcs = controls.FirstPriorityGT(BulletControl.POST_DIR_PRIORITY);
            int numPcs = controls.Count;
            //Note on optimization: keeping accDelta in SB is faster(!) than either a local variable or a SBInProgress struct.
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sb = ref Data[ii];
                    nextDT = ETime.FRAME_TIME;
                    
                    for (int pi = 0; pi < postVelPcs; ++pi) 
                        controls[pi].action(this, ii, sb.bpi, controls[pi].cT);
                    
                    //in nextDT is a significant optimization
                    sb.movement.UpdateDeltaAssignAcc(ref sb.bpi, out sb.accDelta, in nextDT);
                    if (sb.scaleFunc != null)
                        sb.scale = sb.scaleFunc(sb.bpi);
                    
                    //See Bullet Notes > Colliding Pool Controls for details
                    for (int pi = postVelPcs; pi < postDirPcs; ++pi) 
                        controls[pi].action(this, ii, sb.bpi, controls[pi].cT);
                    
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
                        controls[pi].action(this, ii, sb.bpi, controls[pi].cT);
                }
            }
            PruneControls();
        }

        public virtual CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            var allowCameraCull = BC.AllowCameraCull.Value;
            var cullRad = BC.CULL_RAD.Value;
            int graze = 0;
            int collisionDamage = 0;
            Profiler.BeginSample("CheckCollision");
            for (int ii = 0; ii < count; ++ii) {
                // During velocity iteration, bullet controls may destroy some items, so we need to do null checks.
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    bool checkGraze = false;
                    if (sbn.grazeFrameCounter-- == 0) {
                        sbn.grazeFrameCounter = 0;
                        checkGraze = true;
                    }
                    CollisionResult cr = CheckGrazeCollision(in hitbox, ref sbn);
                    if (cr.collide) {
                        collisionDamage = BC.damageAgainstPlayer;
                        if (BC.destructible) {
                            MakeCulledCopy(ii);
                            DeleteSB_Collision(ii);
                        }
                    } else if (checkGraze && cr.graze) {
                        sbn.grazeFrameCounter = BC.grazeEveryFrames;
                        ++graze;
                    } else if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(cullRad, sbn.bpi.loc)) {
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
            var allowCameraCull = BC.AllowCameraCull.Value;
            var cullRad = BC.CULL_RAD.Value;
            for (int ii = 0; ii < count; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(cullRad, sbn.bpi.loc)) {
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
        public virtual void CheckCollision(IReadOnlyList<Enemy.FrozenCollisionInfo> fci) {
            int fciL = fci.Count;
            var cullRad = BC.CULL_RAD.Value;
            for (int ii = 0; ii < count; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    if ((++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(cullRad, sbn.bpi.loc)) {
                        DeleteSB(ii);
                    } else if (sbn.bpi.ctx.playerBullet.Try(out var de) && (de.data.bossDmg > 0 || de.data.stageDmg > 0)) {
                        for (int ff = 0; ff < fciL; ++ff) {
                            if (fci[ff].Active && 
                                BC.cc.collider.CheckCollision(sbn.bpi.loc, sbn.direction, sbn.scale, fci[ff].pos, fci[ff].radius)) {
                                if (BC.destructible || fci[ff].enemy.TryHitIndestructible(sbn.bpi.id, BC.againstEnemyCooldown)) {
                                    fci[ff].enemy.QueuePlayerDamage(de.data.bossDmg, de.data.stageDmg, de.firer);
                                    fci[ff].enemy.ProcOnHit(de.data.effect, sbn.bpi.loc);
                                    if (BC.destructible) {
                                        DeleteSB_Collision(ii);
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

        #endregion
        
        #region State
        
        public void PruneControlsCancellation() {
            //It's not correct to run controls that have been cancelled.
            // As such, we prune for cancellation before execution, and then cancellation/persistence after execution.
            void Prune(DMCompactingArray<BulletControl> carr) {
                for (int ii = 0; ii < carr.Count; ++ii)
                    if (carr[ii].cT.Cancelled)
                        carr.Delete(ii);
                carr.Compact();
            }
            Prune(controls);
            Prune(onCollideControls);
        }
        public void PruneControls() {
            void Prune(DMCompactingArray<BulletControl> carr) {
                for (int ii = 0; ii < carr.Count; ++ii)
                    if (carr[ii].cT.Cancelled || !carr[ii].persist(ParametricInfo.Zero))
                        carr.Delete(ii);
                carr.Compact();
            }
            Prune(controls);
            Prune(onCollideControls);
        }
        public void ClearControls() {
            controls.Empty();
            onCollideControls.Empty();
        }

        public void AssertControls(IReadOnlyList<BulletControl> new_controls) {
            int ci = 0;
            for (int pi = 0; pi < controls.Count && ci < new_controls.Count; ++pi) {
                if (controls[pi] == new_controls[ci]) ++ci;
            }
            //All controls matched
            if (ci == new_controls.Count) return;
            //No controls matched
            if (ci == 0) {
                for (int ii =0; ii < new_controls.Count; ++ii) AddBulletControl(new_controls[ii]);
                return;
            }
            //Some controls matched (?!)
            throw new Exception("AssertControls found that some, neither all nor none, of controls were matched.");
        }

        public void Reset() {
            for (int ii = 0; ii < count; ++ii) {
                if (!rem[ii])
                    DeleteSB(ii);
            }
            // This should free links to BPY/VTP constructed by SMs going out of scope
            Empty();
            temp_last = 0;
        }
        
        #endregion

        #region Renderers
        private void LegacyRender(Camera c, BulletManager bm, int layer) {
            if (BC.Recolor.Value.Try(out var rc)) {
                LegacyRenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            var scaleMin = Fade.scaleInStart;
            var scaleTime = Fade.scaleInTime;
            for (int ii = 0; ii < Count;) {
                int ib = 0;
                for (; ib < batchSize && ii < Count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref Data[ii];
                        ref var m = ref bm.matArr[ib];
                        //Normally handled via FT_SCALE_IN / SCALEIN macro, but that doesn't work for matrix setup
                        var scale = scaleTime > 0 ? 
                            sb.scale * (scaleMin + (1 - scaleMin) *
                                M.Smoothstep(0, scaleTime, sb.bpi.t)) : 
                            sb.scale;
                        m.m00 = m.m11 = sb.direction.x * scale;
                        m.m10 = sb.direction.y * scale;
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
                        ref SimpleBullet sb = ref Data[ii];
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
            if (BC.Recolor.Value.Try(out var rc)) {
                RenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            for (int ii = 0; ii < count;) {
                int ib = 0;
                for (; ib < batchSize && ii < count; ++ii) {
                    if (!rem[ii]) {
                        ref SimpleBullet sb = ref Data[ii];
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
                        ref SimpleBullet sb = ref Data[ii];
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
        
        #endregion
        
#if UNITY_EDITOR
        public int NumControls => controls.Count;
        public object ControlAt(int ii) => controls[ii];
#endif
    }

    private class EmptySBC : SimpleBulletCollection {
        public override CollectionType MetaType => CollectionType.Empty;
        
        public EmptySBC(BulletInCode bc) : base(activeEmpty, bc) { }

        public override CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            throw new Exception("Do not call CheckCollision on empty bullet pools");
        }
        public override void CheckCollision(IReadOnlyList<Enemy.FrozenCollisionInfo> fci) { 
            throw new Exception("Do not call CheckCollision on empty bullet pools");
        }
    }

    /// <summary>
    /// When a bullet is culled, it may "fade out" instead of disappearing. This class is for handling
    /// those afterimages. It will not perform velocity or collision checks,
    /// and it ignores pool commands. It only updates bullet times and culls bullets after some time.
    /// </summary>
    public sealed class CulledBulletCollection : SimpleBulletCollection {
        // ReSharper disable once NotAccessedField.Local
        private readonly SimpleBulletCollection src;
        protected override SimpleBulletFader Fade { get; }
        public override CollectionType MetaType => CollectionType.Culled;

        public CulledBulletCollection(SimpleBulletCollection source) : base(activeCulled, source.CopyBC($"$culled_{source.Style}")) {
            src = source;
            AddSimpleStyle(this);
            BC.UseExitFade();
            Fade = BC.FadeOut;
        }
        
        public override void MakeCulledCopy(int ii) {
            throw new Exception($"Culled SBCs are not enabled for softculling");
        }

        public override void UpdateVelocityAndControls() {
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    //yes, it's supposed to be minus, we are going backwards to get fadeout effect
                    sbn.bpi.t -= ETime.FRAME_TIME;
                    if (sbn.bpi.t < 0) {
                        DeleteSB(ii);
                    }
                }
            }
        }
        
        public override void Add(ref SimpleBullet sb, bool isNew) {
            throw new Exception("Do not call Add for a CulledBulletCollection. Use AddCulled instead.");
        }

        public void AddCulled(ref SimpleBullet sb) {
            Activate();
            var sbn = new SimpleBullet(ref sb);
            sbn.bpi.t = Math.Min(sbn.bpi.t, Fade.MaxTime);
            //scale/dir/etc remain the same.
            base.Add(ref sbn, false);
        }

        public override CollisionCheckResults CheckCollision(in Hitbox hitbox) {
            throw new Exception("Do not call CheckCollision on culled bullet pools");
        }
        public override void CheckCollision(IReadOnlyList<Enemy.FrozenCollisionInfo> fci) { 
            throw new Exception("Do not call CheckCollision on culled bullet pools");
        }
        
    }
    /// <summary>
    /// This class is for bullets that have been soft-culled. Specifically, it handles the extra animation
    /// that is played on top of the bullet, such as cwheel. It will not perform velocity or collision checks,
    /// and it ignores pool commands. It only updates bullet times and culls bullets after some time.
    /// </summary>
    private class DummySoftcullSBC : SimpleBulletCollection {
        private readonly float ttl;
        private readonly float timeR;
        private readonly float rotR;
        public override CollectionType MetaType => CollectionType.Softcull;
        public DummySoftcullSBC(List<SimpleBulletCollection> target, BulletInCode bc, float ttl, float timeR, float rotR) : base(target, bc) {
            this.ttl = ttl;
            this.timeR = timeR;
            this.rotR = rotR / 2f;
        }
        
        public override void MakeCulledCopy(int ii) {
            throw new Exception("Softcull SBCs are not enabled for softculling");
        }

        public override void Add(ref SimpleBullet sb, bool isNew) {
            sb.bpi.t += RNG.GetFloat(0, this.timeR);
            sb.direction = M.RotateVectorDeg(sb.direction, RNG.GetFloat(-rotR, rotR));
            base.Add(ref sb, false);
        }

        public override void UpdateVelocityAndControls() {
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!rem[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
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
        public override void CheckCollision(IReadOnlyList<Enemy.FrozenCollisionInfo> fci) { 
            Compact();
        }
    }
    private class CircleSBC : SimpleBulletCollection {
        public CircleSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnCircle(in hitbox, sb.bpi.loc, BC.cc.radius * sb.scale);
    }
    private class RectSBC : SimpleBulletCollection {
        public RectSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRect(in hitbox, sb.bpi.loc, BC.cc.halfRect.x, 
                BC.cc.halfRect.y, BC.cc.maxDist2, sb.scale, sb.direction.x, sb.direction.y);
    }
    private class LineSBC : SimpleBulletCollection {
        public LineSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRotatedSegment(in hitbox, sb.bpi.loc, BC.cc.radius, BC.cc.linePt1, 
                BC.cc.delta, sb.scale, BC.cc.deltaMag2, BC.cc.maxDist2, sb.direction.x, sb.direction.y);
    }
    private class NoCollSBC : SimpleBulletCollection {
        public NoCollSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override CollisionResult CheckGrazeCollision(in Hitbox hitbox, ref SimpleBullet sb) 
            => CollisionResult.noColl;
    }

    //Called via Camera.onPreCull event
    private void RenderBullets(Camera c) {
        if (!Application.isPlaying) { return; }
        RNG.RNG_ALLOWED = false;
        SimpleBulletCollection sbc;
        if ((c.cullingMask & ppLayerMask) != 0) {
            for (int ii = 0; ii < activePlayer.Count; ++ii) {
                sbc = activePlayer[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, ppRenderLayer);
            }
        }
        if ((c.cullingMask & epLayerMask) != 0) {
            //empty bullets are not rendered
            for (int ii = 0; ii < activeCulled.Count; ++ii) {
                sbc = activeCulled[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, epRenderLayer);
            }
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                sbc = activeNpc[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, epRenderLayer);
            }
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                sbc = activeCNpc[ii];
                if (sbc.Count > 0) sbc.SwitchRender(c, this, epRenderLayer);
            }
        }
        RNG.RNG_ALLOWED = true;
    }

    private void CallLegacyRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        if (ct == 0) return;
        UnityEngine.Graphics.DrawMeshInstanced(ri.Mesh, 0, ri.Material,
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
        UnityEngine.Graphics.DrawMeshInstancedProcedural(ri.Mesh, 0, ri.Material,
          bounds: drawBounds,
          count: ct,
          properties: pb,
          castShadows: ShadowCastingMode.Off,
          receiveShadows: false,
          layer: layer,
          camera: c);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug FCTX usage")]
    public void DebugFCTX() {
        Logs.Log($"Alloc {FiringCtx.Allocated} / Popped {FiringCtx.Popped} / Cached {FiringCtx.Recached} / Copied {FiringCtx.Copied}");
    }
    [ContextMenu("Debug bullet numbers")]
    public void DebugBulletNums() {
        int total = 0;
        foreach (var pool in simpleBulletPools.Values) {
            total += pool.Count;
            if (pool.Count > 0) Logs.Log($"{pool.Style}: {pool.Count} (-{pool.NullElements})", level: LogLevel.INFO);
            if (pool.NumControls > 0) Logs.Log($"{pool.Style} has {pool.NumControls} controls", level: LogLevel.INFO);
        }
        total += Bullet.NumBullets;
        Logs.Log($"Custom pools: {string.Join(", ", activeCNpc.Select(x => x.Style))}");
        Logs.Log($"Empty pools: {string.Join(", ", activeEmpty.Select(x => x.Style))}");
        Logs.Log($"Fancy bullets: {Bullet.NumBullets}", level: LogLevel.INFO);
        Logs.Log($"Total: {total}", level: LogLevel.INFO);
    }
    #endif
}

}