using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Sorting;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Pooling;
using Danmokou.Reflection2;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Danmokou.Danmaku {

public partial class BulletManager {
    private void SetupRendering() {
        AddToken(renderPropsCBP);
        AddToken(renderPropsTintCBP);
        AddToken(renderPropsRecolorCBP);
    }
    /// <summary>
    /// Struct passed to simple bullet instanced rendering containing per-bullet critical information.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RenderProperties {
        public Vector2 position;
        public Vector2 direction;
        public float time;
        public static readonly int Size = UnsafeUtility.SizeOf<RenderProperties>();
    }
    /// <summary>
    /// Struct passed to simple bullet instanced rendering containing per-bullet recolorizable pool information.
    /// <br/>Only passed for recolorizable pools.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RenderPropertiesRecolor {
        public Vector4 recolorB;
        public Vector4 recolorW;

        public static readonly int Size = UnsafeUtility.SizeOf<RenderPropertiesRecolor>();
    }

    //Rendering variables
    private const int batchSize = 2047;
    //Note: while 1023 is the maximum shader array length,
    // the legacy renderer will (dangerously) split calls of batches greater than 511.
    private const int legacyBatchSize = 511;
    private MaterialPropertyBlock pb = null!;
    private readonly RenderProperties[] renderPropsArr = new RenderProperties[batchSize];
    //Compute buffers make rendering 20-30% faster over directly using shader arrays
    private readonly ComputeBufferPool renderPropsCBP = new(batchSize, RenderProperties.Size);
    private readonly Vector4[] tintArr = new Vector4[batchSize];
    private readonly ComputeBufferPool renderPropsTintCBP = new(batchSize, sizeof(float) * 4);
    private readonly RenderPropertiesRecolor[] recolorArr = new RenderPropertiesRecolor[batchSize];
    private readonly ComputeBufferPool renderPropsRecolorCBP = new(batchSize, RenderPropertiesRecolor.Size);
    private static readonly int renderPropsId = Shader.PropertyToID("_Properties");
    private static readonly int renderPropsTintId = Shader.PropertyToID("_PropertiesTint");
    private static readonly int renderPropsRecolorId = Shader.PropertyToID("_PropertiesRecolor");
    private static readonly int posDirPropertyId = Shader.PropertyToID("posDirBuffer");
    private static readonly int tintPropertyId = Shader.PropertyToID("tintBuffer");
    private static readonly int timePropertyId = Shader.PropertyToID("timeBuffer");
    private static readonly int recolorBPropertyId = Shader.PropertyToID("recolorBBuffer");
    private static readonly int recolorWPropertyId = Shader.PropertyToID("recolorWBuffer");
    private static readonly Bounds drawBounds = new(Vector3.zero, Vector3.one * 1000f);
    private readonly Vector4[] posDirArr = new Vector4[legacyBatchSize];
    private readonly Vector4[] tintArrLegacy = new Vector4[legacyBatchSize];
    private readonly Vector4[] recolorBArr = new Vector4[legacyBatchSize];
    private readonly Vector4[] recolorWArr = new Vector4[legacyBatchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[legacyBatchSize];
    private readonly float[] timeArr = new float[legacyBatchSize];
    
    private const ushort CULL_EVERY_MASK = 127;
    /// <summary>
    /// A container for all information about a code-abstraction bullet except style information.
    /// </summary>
    public struct SimpleBullet {
        //96 byte struct. (94 unpacked)
            //BPY  = 8
            //TP   = 8
            //VS   = 32
            //V3   = 12
            //BPI  = 20
            //Flt  = 4
            //V2   = 8
            //S    = 2
        public readonly BPY? scaleFunc;
        public readonly SBV2? dirFunc;
        public Movement movement; //Don't make this readonly
        /// <summary>
        /// Accumulated position delta for each frame.
        /// Currently, this is only used for direction, and
        /// the delta is also put into BPI when this is generated.
        /// </summary>
        public Vector3 accDelta;
        [UsedImplicitly]
        public Vector2 AccDeltaV2 => accDelta;
        public ParametricInfo bpi;
        public float scale;
        public Vector2 direction;
        
        public ushort cullFrameCounter;

        public SimpleBullet(BPY? scaleF, SBV2? dirF, in Movement movement, ParametricInfo bpi) {
            scaleFunc = scaleF;
            dirFunc = dirF;
            scale = 1f;
            this.movement = movement;
            cullFrameCounter = 0;
            this.accDelta = Vector2.zero;
            this.bpi = bpi;
            direction = this.movement.UpdateZero(ref this.bpi);
            scale = scaleFunc?.Invoke(this.bpi) ?? 1f;
            if (dirFunc != null) direction = dirFunc(ref this);
        }

        /// <summary>
        /// Constructor for copying a SimpleBullet. The BPI id is copied as well if <see cref="newId"/> is null.
        /// </summary>
        public SimpleBullet(ref SimpleBullet sb, uint? newId = null) {
            scaleFunc = sb.scaleFunc;
            dirFunc = sb.dirFunc;
            scale = sb.scale;
            movement = sb.movement;
            cullFrameCounter = 0;
            accDelta = sb.accDelta;
            bpi = sb.bpi.CopyCtx(newId ?? sb.bpi.id);
            direction = sb.direction;
        }
    }

    public readonly struct FrameBucketing {
        //this would be more efficient for MCD calcs if you paired it to bucket, but that's probably too computationally expensive
        public readonly float maxScale;
        
        public FrameBucketing(float maxScale) {
            this.maxScale = maxScale;
        }
    }

    //Instantiate this class directly for player bullets
    public class SimpleBulletCollection: CompactingArray<SimpleBullet> {
        public const int PARALLEL_CUTOFF = 16384;
        public const bool PARALLELISM_ENABLED = false;
        
        //Draw elements with higher Z first, in accordance with Unity left-handedness
        private static readonly LeqCompare<SimpleBullet> ZCompare =
            (in SimpleBullet sb1, in SimpleBullet sb2) =>
                sb1.bpi.loc.z >= sb2.bpi.loc.z;
        private SimpleBullet[]? zSortBuffer = null;
        public enum CollectionType {
            /// <summary>
            /// Empty bullets (no display or collision, used for guiding; player variants and copies included)
            /// </summary>
            Empty,
            Normal,
            /// <summary>
            /// Bullets created to represent trivial items when normal bullets are cleared via bombs or photos. These home to the player and add points when they collide with the player.
            /// </summary>
            BulletClearFlake,
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
        
        /// <summary>
        /// True iff this is a pool of simple bullets fired by a player character.
        /// </summary>
        public bool IsPlayer { get; private set; } = false;
        public BulletInCode BC { get; }
        protected readonly CollidableInfo ColliderGeneric;
        public readonly ICollider Collider;
        public readonly ApproximatedCircleCollider CircleCollider;
        protected virtual SimpleBulletFader Fade => BC.FadeIn;
        private readonly List<SimpleBulletCollection> targetList;
        public int temp_last;
        /// <summary>
        /// Copied pools (including player pools) have this set
        /// </summary>
        private SimpleBulletCollection? original;
        private CulledBulletCollection? culled;
        protected readonly DMCompactingArray<BulletControl> controls = new(4);
        private readonly DMCompactingArray<BulletControl> onCollideControls = new(2);
        private readonly AnyTypeDMCompactingArray<IDeletionMarker> bucketingRequests = new(4);
        private List<int>[]? buckets;
        public ReadOnlySpan2D<List<int>> bucketsSpan => new(buckets!, bucketsY, bucketsX);

        /// <summary>
        /// Find the buckets spanned by the AABB between the minimum and maximum locations.
        /// <br/>This DOES NOT adjust for the bullet collider radius; the caller must do so.
        /// </summary>
        public ReadOnlySpan2D<List<int>> BucketsSpanForPosition(Vector2 minLoc, Vector2 maxLoc) {
            var minBucket = BucketIndexPair(minLoc);
            var maxBucket = BucketIndexPair(maxLoc);
            return bucketsSpan[new Range(minBucket.y, maxBucket.y + 1), new Range(minBucket.x, maxBucket.x + 1)];
        }
        
        //This is null when bucketing did not take place this frame
        private FrameBucketing? frameBucket;
        private int bucketsX;
        private int bucketsY;
        private float invBucketSize;

        public virtual CollectionType MetaType => CollectionType.Normal;
        public string Style => BC.name;
        public TP4? Tint => BC.Tint.Value;
        public virtual (TP4 black, TP4 white)? Recolor => BC.Recolor.Value;
        public bool SubjectToAutocull =>
            !IsPlayer && MetaType == CollectionType.Normal;
        public bool IsCopy => original != null;
        //Player bullets need their own culled pools to handle the opacity multiplier
        public CulledBulletCollection Culled => 
            (IsPlayer ? null : original)?.Culled ?? (culled ??= new CulledBulletCollection(this));
        
        public SimpleBulletCollection(List<SimpleBulletCollection> target, BulletInCode bc) : base(1, 128) {
            this.BC = bc;
            ColliderGeneric = bc.cc;
            Collider = ColliderGeneric.collider;
            CircleCollider = ColliderGeneric.circleCollider;
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
                if (targetList == activeNpc && NPCBulletsRequireBucketing) {
                    CreateBuckets();
                    frameBucket = new(1);
                }
                Logs.Log($"Activating pool {Style}", level: LogLevel.DEBUG1);
                Active = true;
            }
        }
        public void Deactivate() {
            Active = false;
            temp_last = 0;
            buckets = null;
        }
        
        #region Copiers

        public BulletInCode CopyBC(string newPool) => BC.Copy(newPool);
        public SimpleBulletCollection CopySimplePool(List<SimpleBulletCollection> target, string newPool) => new(target, BC.Copy(newPool));
        public SimpleBulletCollection CopyPool(List<SimpleBulletCollection> target, string newPool) => GetCollection(target, BC.Copy(newPool));
        
        #endregion

        public MeshGenerator.RenderInfo GetOrLoadRI() => BC.GetOrLoadRI();
        
        /// <summary>
        /// Test collision between a target and a bullet.
        /// <br/>Note that you can also use <see cref="Collider"/>.<see cref="ICollider.CheckGrazeCollision"/>, which is marginally slower
        ///  but may generally be more flexible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual CollisionResult CheckGrazeCollision(in Hurtbox hurtbox, in SimpleBullet sb) =>
            CollisionMath.NoCollision;

        #region Operators
        
        //TODO: investigate if isNew is actually required; is it possible to always apply the initial controls?
        public virtual void Add(ref SimpleBullet sb, bool isNew) {
            base.AddRef(ref sb);
            if (isNew) {
                int numPcs = controls.Count;
                var state = new VelocityUpdateState(this, 0, 0) { ii = count - 1 };
                for (int pi = 0; pi < numPcs && !Deleted[state.ii]; ++pi) {
                    controls[pi].action(in state, sb.bpi, controls[pi].cT);
                }
            }
            //Bullets are eligible for collision on frame 0, so if bucketing has already occured this frame,
            // then bucket this bullet appropriately
            if (frameBucket.Try(out var fb)) {
                buckets![BucketIndex(in sb.bpi.loc)].Add(Count - 1);
                if (sb.scale > fb.maxScale)
                    frameBucket = new(sb.scale);
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
            using var gcx = bpi.ctx.RevertToGCX(target.Scope, inode);
            _ = inode.RunExternalSM(SMRunner.Cull(target, cT, gcx));
            return inode;
        }
        
        /// <summary>
        /// Transfer a bullet from another pool. Note this preserves the ID.
        /// </summary>
        [UsedImplicitly]
        public void TransferFrom(SimpleBulletCollection sbc, int ii) {
            //TODO this could theoretically be optimized to actually transfer the SimpleBullet
            // instead of creating a new one and destroying the original
            var sb = new SimpleBullet(ref sbc[ii]);
            Add(ref sb, false);
            sbc.DeleteSB(ii);
        }
        
        [UsedImplicitly]
        public void CopyNullFrom(SimpleBulletCollection sbc, int ii, SoftcullProperties? advancer) =>
            RequestNullSimple(Style, sbc[ii].bpi.loc, sbc[ii].direction, advancer?.AdvanceTime(sbc[ii].bpi.loc) ?? 0f);

        /// <summary>
        /// Copy a bullet from another pool. Note this reassigns the ID.
        /// </summary>
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
            if (!Deleted[ind]) {
                Data[ind].bpi.Dispose();
                Delete(ind);
            }
        }

        public void RunCollisionControls(int ind) {
            if (!Deleted[ind]) {
                ref var sb = ref Data[ind];
                var st = new VelocityUpdateState(this, 0, 0) { ii = ind };
                for (int ii = 0; ii < onCollideControls.Count; ++ii) {
                    onCollideControls[ii].action(in st, in sb.bpi, in onCollideControls[ii].cT);
                }
            }
        }
        
        #endregion

        #region Updaters
        
        [UsedImplicitly] //batch command uses this to stop when a bullet is destroyed
        public bool IsAlive(int ind) => !Deleted[ind];
        
        public struct VelocityUpdateState {
            public readonly SimpleBulletCollection sbc;
            public readonly int postVelPcs;
            public readonly int postDirPcs;
            public float nextDT;
            public int ii;
            public VelocityUpdateState(SimpleBulletCollection sbc, int postVelPcs, int postDirPcs) {
                this.sbc = sbc;
                this.postVelPcs = postVelPcs;
                this.postDirPcs = postDirPcs;
                this.nextDT = ETime.FRAME_TIME;
                this.ii = 0;
            }
            
            [UsedImplicitly]
            public void Speedup(float ratio) => nextDT *= ratio;
        }
        private void VelocityProcessBatch(int start, int end, VelocityUpdateState state) {
            var numPcs = controls.Count;
            var allowCameraCull = BC.AllowCameraCull.Value;
            var cullRad = BC.CullRadius.Value;
            //Note that state.ii is also consumed by controls that need to do indexed deletion;
            // you can't replace this with a local without adding another argument to SBCF
            for (state.ii = start; state.ii < end; ++state.ii) {
                if (!Deleted[state.ii]) {
                    ref var sb = ref Data[state.ii];
                    state.nextDT = ETime.FRAME_TIME;
                    
                    for (int pi = 0; pi < state.postVelPcs; ++pi) 
                        controls[pi].action(in state, in sb.bpi, in controls[pi].cT);
                    
                    //Note on optimization: keeping accDelta in SB is faster(!) than either a local variable or a SBInProgress struct.
                    sb.movement.UpdateDeltaAssignDelta(ref sb.bpi, ref sb.accDelta, in state.nextDT);
                    if (sb.scaleFunc != null)
                        sb.scale = sb.scaleFunc(sb.bpi);
                    
                    for (int pi = state.postVelPcs; pi < state.postDirPcs; ++pi) 
                        controls[pi].action(in state, in sb.bpi, in controls[pi].cT);
                    
                    if (sb.dirFunc == null) {
                        float mag = sb.accDelta.x * sb.accDelta.x + sb.accDelta.y * sb.accDelta.y;
                        if (mag > M.MAG_ERR) {
                            mag = 1f / (float) Math.Sqrt(mag);
                            sb.direction.x = sb.accDelta.x * mag;
                            sb.direction.y = sb.accDelta.y * mag;
                        }
                    } else
                        sb.direction = sb.dirFunc(ref sb);
                    
                    //TODO there's technically an inconsistency in ordering when parallelism is enabled or not of when culling is done relative to post-dir ctrls
                    //this is a problem if parallelism is considered a per-computer setting
                    if (!PARALLELISM_ENABLED)
                        //Post-vel controls may destroy the bullet. As soon as this occurs, stop iterating
                        for (int pi = state.postDirPcs; pi < numPcs && !Deleted[state.ii]; ++pi) 
                            controls[pi].action(in state, in sb.bpi, in controls[pi].cT);
                    
                    //Don't check Deleted[state.ii] here for efficiency. Double-deletion is safe
                    if (allowCameraCull && (++sb.cullFrameCounter & CULL_EVERY_MASK) == 0 &&
                        LocationHelpers.OffPlayableScreenBy(in cullRad, in sb.bpi.loc)) {
                        DeleteSB(state.ii);
                    }
                }
            }
        }
        private VelocityUpdateState VelocityParallelize(VelocityUpdateState state) {
            RNG.RNG_ALLOWED = false;
            var p = Partitioner.Create(0, count);
            Parallel.ForEach(p, range => VelocityProcessBatch(range.Item1, range.Item2, state));
            RNG.RNG_ALLOWED = true;
            return state;
        }
        private void UpdateVelocityAndControlsNonBucketed() {
            PruneControlsCancellation();
            if (Count > 0) {
                var state = new VelocityUpdateState(this,
                    controls.FirstPriorityGT(BulletControl.POST_VEL_PRIORITY),
                    controls.FirstPriorityGT(BulletControl.POST_DIR_PRIORITY));
                Profiler.BeginSample("Core velocity step");
                if (!PARALLELISM_ENABLED || temp_last < PARALLEL_CUTOFF)
                    VelocityProcessBatch(0, temp_last, state);
                else
                    VelocityParallelize(state);
                Profiler.EndSample();

                //Post-dir controls can't be parallelized, so they need to be moved out of the parallel loop
                if (PARALLELISM_ENABLED) {
                    int numPcs = controls.Count;
                    Profiler.BeginSample("Non-parallelizable controls update");
                    if (state.postDirPcs < numPcs) {
                        for (state.ii = 0; state.ii < temp_last; ++state.ii) {
                            ref var sb = ref Data[state.ii];
                            //Post-vel controls may destroy the bullet. As soon as this occurs, stop iterating
                            for (int pi = state.postDirPcs; pi < numPcs && !Deleted[state.ii]; ++pi)
                                controls[pi].action(in state, sb.bpi, controls[pi].cT);
                        }
                    }
                    Profiler.EndSample();
                }
            }
            PruneControls();
        }

        public virtual void UpdateVelocityAndControls(bool forceBucketing=false) {
            bucketingRequests.Compact();
            if (bucketingRequests.Count > 0 || IsPlayer || forceBucketing)
                UpdateVelocityAndControlsBucketed();
            else
                UpdateVelocityAndControlsNonBucketed();
        }
        
        public void CompactAndSort() {
            //This must be done at end of frame because bucketed collisions rely on index being consistent
            if (NullElements > Math.Min(2000, Count / 10) || BC.UseZCompare) {
                Profiler.BeginSample("Compact");
                Compact();
                Profiler.EndSample();
            }
            EmptyBuckets();
            if (BC.UseZCompare) {
                Profiler.BeginSample("Z-sort");
                var reqLen = (count + 1) / 2;
                if (zSortBuffer == null || zSortBuffer.Length < reqLen)
                    zSortBuffer = new SimpleBullet[(Data.Length + 1) / 2];
                //Since the array is sorted every frame, it is always "mostly sorted"
                //This makes CombMergeSort good
                CombMergeSorter<SimpleBullet>.Sort(Data, 0, count, ZCompare, zSortBuffer);
                Profiler.EndSample();
            }
        }

        public void CheckCollisions(ISimpleBulletCollisionReceiver recv) {
            if (GetCollisionFormat().Try(out var fmt)) {
                if (fmt.Valid) {
                    recv.ProcessSimpleBucketed(this, fmt.Value);
                } else
                    recv.ProcessSimple(this);
            } //else pass
        }

        /// <summary>
        /// Get the method by which collision should be checked on this bullet pool for this frame.
        /// </summary>
        /// <returns>Some(bucketing) for bucketed collisions, None for non-bucketed collisions, and null for no collision.</returns>
        public virtual Maybe<FrameBucketing>? GetCollisionFormat() =>
            frameBucket.Try(out var f) ? Maybe<FrameBucketing>.Of(f) : Maybe<FrameBucketing>.None; 
        
        #region Bucketing

        public void RequestBucketing(IDeletionMarker tracker) => bucketingRequests.AddPriority(tracker);

        private void CreateBuckets() {
            if (buckets != null) return;
            frameBucket = null;
            invBucketSize = 2f; //TODO
            //Add one extra bucket layer on each side (left and right) to handle offscreen objects
            bucketsX = Mathf.CeilToInt(LocationHelpers.Width * invBucketSize) + 2;
            bucketsY = Mathf.CeilToInt(LocationHelpers.Height * invBucketSize) + 2;
            buckets = new List<int>[bucketsX * bucketsY];
            for (int ii = 0; ii < buckets.Length; ++ii)
                buckets[ii] = new();
        }

        private void EmptyBuckets() {
            if (buckets == null) return;
            for (int ii = 0; ii < buckets.Length; ++ii)
                buckets[ii].Clear();
            frameBucket = null;
        }

        public (int x, int y) BucketIndexPair(in Vector2 loc) {
            // -1 at end is because we add extra buckets (one layer on each side) for offscreen stuff
            int xIndex = M.Clamp(0, bucketsX - 1, (int)Math.Floor((loc.x - LocationHelpers.Left) * invBucketSize) + 1);
            int yIndex = M.Clamp(0, bucketsY - 1, (int)Math.Floor((loc.y - LocationHelpers.Bot) * invBucketSize) + 1);
            return (xIndex, yIndex);
        }
        private int BucketIndex(in Vector3 loc) {
            // +1 at end is because we add extra buckets (one layer on each side) for offscreen stuff
            int xIndex = M.Clamp(0, bucketsX - 1, (int)Math.Floor((loc.x - LocationHelpers.Left) * invBucketSize) + 1);
            int yIndex = M.Clamp(0, bucketsY - 1, (int)Math.Floor((loc.y - LocationHelpers.Bot) * invBucketSize) + 1);
            return xIndex + bucketsX * yIndex;
        }

        private void UpdateVelocityAndControlsBucketed() {
            CreateBuckets();
            PruneControlsCancellation();
            var state = new VelocityUpdateState(this,
                controls.FirstPriorityGT(BulletControl.POST_VEL_PRIORITY),
                controls.FirstPriorityGT(BulletControl.POST_DIR_PRIORITY));
            int numPcs = controls.Count;
            var cullRad = BC.CullRadius.Value;
            float maxScale = 1;
            //Also, we do camera culling here, as it's too expensive to do it in collision.
            Profiler.BeginSample("Core velocity step (bucketed)");
            for (state.ii = 0; state.ii < temp_last; ++state.ii) {
                if (!Deleted[state.ii]) {
                    ref var sb = ref Data[state.ii];
                    state.nextDT = ETime.FRAME_TIME;
                    
                    for (int pi = 0; pi < state.postVelPcs; ++pi) 
                        controls[pi].action(in state, in sb.bpi, in controls[pi].cT);
                    
                    //Note on optimization: keeping accDelta in SB is faster(!) than either a local variable or a SBInProgress struct.
                    sb.movement.UpdateDeltaAssignDelta(ref sb.bpi, ref sb.accDelta, in state.nextDT);
                    if (sb.scaleFunc != null && (sb.scale = sb.scaleFunc(sb.bpi)) > maxScale)
                        maxScale = sb.scale;
                    
                    for (int pi = state.postVelPcs; pi < state.postDirPcs; ++pi) 
                        controls[pi].action(in state, in sb.bpi, in controls[pi].cT);
                    
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
                    for (int pi = state.postDirPcs; pi < numPcs && !Deleted[state.ii]; ++pi) 
                        controls[pi].action(in state, sb.bpi, controls[pi].cT);

                    if (!Deleted[state.ii]) {
                        if ((++sb.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationHelpers.OffPlayableScreenBy(cullRad, sb.bpi.loc))
                            DeleteSB(state.ii);
                        else
                            buckets![BucketIndex(in sb.bpi.loc)].Add(state.ii);
                    }
                }
            }
            Profiler.EndSample();
            frameBucket = new(maxScale);
            PruneControls();
            
        }

        public void DebugBuckets() {
            var sb = new StringBuilder();
            sb.Append($"{bucketsX}*{bucketsY}={buckets!.Length} buckets (size {invBucketSize}\n");
            for (var iy = 0; iy < bucketsY; ++iy)
            for (var ix = 0; ix < bucketsX; ++ix) {
                var b = buckets[ix + iy * bucketsX];
                if (b.Count > 0)
                    sb.Append($"Bucket {ix},{iy}: {b.Count}\n");
            }
            Logs.Log(sb.ToString());
        }
        
        
        
        #endregion

        #endregion
        
        #region State
        
        public void PruneControlsCancellation() {
            //It's not correct to run controls that have been cancelled.
            // As such, we prune for cancellation before execution, and then cancellation/persistence after execution.
            void Prune(DMCompactingArray<BulletControl> carr) {
                for (int ii = 0; ii < carr.Count; ++ii)
                    if (carr[ii].cT.Cancelled) {
                        carr[ii].caller.Dispose();
                        carr.Delete(ii);
                    }
                carr.Compact();
            }
            Prune(controls);
            Prune(onCollideControls);
        }
        public void PruneControls() {
            void Prune(DMCompactingArray<BulletControl> carr) {
                for (int ii = 0; ii < carr.Count; ++ii)
                    if (carr[ii].cT.Cancelled || !carr[ii].persist(carr[ii].caller)) {
                        carr[ii].caller.Dispose();
                        carr.Delete(ii);
                    }
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
                if (!Deleted[ii])
                    DeleteSB(ii);
            }
            // This should free links to BPY/VTP constructed by SMs going out of scope
            Empty();
            zSortBuffer = null;
            temp_last = 0;
        }
        
        #endregion

        #region Renderers
        private void LegacyRender(Camera c, BulletManager bm, int layer) {
            if (Recolor.Try(out var rc)) {
                LegacyRenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            var scaleMin = Fade.scaleInStart;
            var scaleTime = Fade.scaleInTime;
            for (int ii = 0; ii < count;) {
                int ib = 0;
                for (; ib < legacyBatchSize && ii < count; ++ii) {
                    if (!Deleted[ii]) {
                        ref var sb = ref Data[ii];
                        ref var m = ref bm.matArr[ib];
                        //Normally handled via FT_SCALE_IN / SCALEIN macro, but that doesn't work for matrix setup
                        var scale = scaleTime > 0 ? 
                            sb.scale * (scaleMin + (1 - scaleMin) *
                                BMath.Smoothstep(0, scaleTime, sb.bpi.t)) : 
                            sb.scale;
                        m.m00 = m.m11 = sb.direction.x * scale;
                        m.m01 = -(m.m10 = sb.direction.y * scale);
                        m.m22 = m.m33 = 1;
                        m.m03 = sb.bpi.loc.x;
                        m.m13 = sb.bpi.loc.y;
                        if (hasTint) bm.tintArrLegacy[ib] = tint!(sb.bpi);
                        bm.timeArr[ib] = sb.bpi.t;
                        ++ib;
                    }
                }
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArrLegacy);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallLegacyRender(ri, c, layer, ib);
            }
        }
        
        private void LegacyRenderRecolorizable(Camera c, BulletManager bm, int layer, 
            (TP4 black, TP4 white) rc) {
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            var scaleMin = Fade.scaleInStart;
            var scaleTime = Fade.scaleInTime;
            for (int ii = 0; ii < count;) {
                int ib = 0;
                for (; ib < legacyBatchSize && ii < count; ++ii) {
                    if (!Deleted[ii]) {
                        ref var sb = ref Data[ii];
                        ref var m = ref bm.matArr[ib];
                        //Normally handled via FT_SCALE_IN / SCALEIN macro, but that doesn't work for matrix setup
                        var scale = scaleTime > 0 ? 
                            sb.scale * (scaleMin + (1 - scaleMin) *
                                BMath.Smoothstep(0, scaleTime, sb.bpi.t)) : 
                            sb.scale;
                        m.m00 = m.m11 = sb.direction.x * scale;
                        m.m01 = -(m.m10 = sb.direction.y * scale);
                        m.m22 = m.m33 = 1;
                        m.m03 = sb.bpi.loc.x;
                        m.m13 = sb.bpi.loc.y;
                        bm.recolorBArr[ib] = rc.black(sb.bpi);
                        bm.recolorWArr[ib] = rc.white(sb.bpi);
                        if (hasTint) bm.tintArrLegacy[ib] = tint!(sb.bpi);
                        bm.timeArr[ib] = sb.bpi.t;
                        ++ib;
                    }
                }
                bm.pb.SetVectorArray(recolorBPropertyId, bm.recolorBArr);
                bm.pb.SetVectorArray(recolorWPropertyId, bm.recolorWArr);
                if (hasTint) bm.pb.SetVectorArray(tintPropertyId, bm.tintArrLegacy);
                bm.pb.SetFloatArray(timePropertyId, bm.timeArr);
                bm.CallLegacyRender(ri, c, layer, ib);
            }
        }

        private void Render(Camera c, BulletManager bm, int layer) {
            if (Recolor.Try(out var rc)) {
                RenderRecolorizable(c, bm, layer, rc);
                return;
            }
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            for (int ii = 0; ii < count;) {
                int ib = 0;
                for (; ib < batchSize && ii < count; ++ii) {
                    if (!Deleted[ii]) {
                        ref var sb = ref Data[ii];
                        ref var rp = ref bm.renderPropsArr[ib];
                        rp.position.x = sb.bpi.loc.x;
                        rp.position.y = sb.bpi.loc.y;
                        rp.direction.x = sb.direction.x * sb.scale;
                        rp.direction.y = sb.direction.y * sb.scale;
                        rp.time = sb.bpi.t;
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        ++ib;
                    }
                }
                bm.pb.SetBufferFromArray(renderPropsId, bm.renderPropsCBP, bm.renderPropsArr, ib);
                if (hasTint)
                    bm.pb.SetBufferFromArray(renderPropsTintId, bm.renderPropsTintCBP, bm.tintArr, ib);
                bm.CallRender(ri, c, layer, ib);
            }
        }

        private void RenderRecolorizable(Camera c, BulletManager bm, int layer, (TP4 black, TP4 white) rc) {
            var tint = Tint;
            var hasTint = tint != null;
            MeshGenerator.RenderInfo ri = GetOrLoadRI();
            
            for (int ii = 0; ii < count;) {
                int ib = 0;
                for (; ib < batchSize && ii < count; ++ii) {
                    if (!Deleted[ii]) {
                        ref var sb = ref Data[ii];
                        ref var rp = ref bm.renderPropsArr[ib];
                        rp.position.x = sb.bpi.loc.x;
                        rp.position.y = sb.bpi.loc.y;
                        rp.direction.x = sb.direction.x * sb.scale;
                        rp.direction.y = sb.direction.y * sb.scale;
                        bm.recolorArr[ib].recolorB = rc.black(sb.bpi);
                        bm.recolorArr[ib].recolorW = rc.white(sb.bpi);
                        rp.time = sb.bpi.t;
                        if (hasTint) bm.tintArr[ib] = tint!(sb.bpi);
                        ++ib;
                    }
                }
                bm.pb.SetBufferFromArray(renderPropsId, bm.renderPropsCBP, bm.renderPropsArr, ib);
                bm.pb.SetBufferFromArray(renderPropsRecolorId, bm.renderPropsRecolorCBP, bm.recolorArr, ib);
                if (hasTint)
                    bm.pb.SetBufferFromArray(renderPropsTintId, bm.renderPropsTintCBP, bm.tintArr, ib);
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



    private class BulletFlakeSBC : CircleSBC {
        public override CollectionType MetaType => CollectionType.BulletClearFlake;
        
        public BulletFlakeSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) { }
    }
    
    private class EmptySBC : NoCollSBC {
        public override CollectionType MetaType => CollectionType.Empty;
        
        public EmptySBC(BulletInCode bc) : base(activeEmpty, bc) { }
    }

    /// <summary>
    /// When a bullet is culled, it may "fade out" instead of disappearing. This class is for handling
    /// those afterimages. It will not perform velocity or collision checks,
    /// and it ignores pool commands. It only updates bullet times and culls bullets after some time.
    /// </summary>
    public sealed class CulledBulletCollection : NoCollSBC {
        // ReSharper disable once NotAccessedField.Local
        private readonly SimpleBulletCollection src;
        protected override SimpleBulletFader Fade { get; }
        public override CollectionType MetaType => CollectionType.Culled;

        public override (TP4 black, TP4 white)? Recolor => src.Recolor;

        public CulledBulletCollection(SimpleBulletCollection source) : base(activeCulled, source.CopyBC($"$culled_{source.Style}")) {
            src = source;
            AddSimpleStyle(this);
            BC.UseExitFade();
            Fade = BC.FadeOut;
        }
        
        public override void MakeCulledCopy(int ii) {
            throw new Exception($"Culled SBCs are not enabled for softculling");
        }

        public override void UpdateVelocityAndControls(bool forceBucketing=false) {
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!Deleted[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    //yes, it's supposed to be minus, we are going backwards to get fadeout effect
                    var fadeTime = sbn.bpi.t - ETime.FRAME_TIME;
                    if (fadeTime < 0) {
                        DeleteSB(ii);
                    } else {
                        sbn.bpi.ctx.culledBulletTime += ETime.FRAME_TIME;
                        //We only update direction, not scale/movement
                        if (sbn.dirFunc != null) {
                            sbn.bpi.t = sbn.bpi.ctx.culledBulletTime;
                            sbn.direction = sbn.dirFunc(ref sbn);
                        }
                        sbn.bpi.t = fadeTime;
                    }
                }
            }
        }
        
        public override void Add(ref SimpleBullet sb, bool isNew) {
            throw new Exception("Do not call Add for a CulledBulletCollection. Use AddCulled instead.");
        }

        public void AddCulled(ref SimpleBullet sb) {
            if (Fade.MaxTime <= 0) return;
            Activate();
            var sbn = new SimpleBullet(ref sb);
            sbn.bpi.ctx.culledBulletTime = sbn.bpi.t;
            sbn.bpi.t = Math.Min(sbn.bpi.t, Fade.MaxTime);
            base.Add(ref sbn, false);
        }
        
    }
    /// <summary>
    /// This class is for bullets that have been soft-culled. Specifically, it handles the extra animation
    /// that is played on top of the bullet, such as cwheel. It will not perform velocity or collision checks,
    /// and it ignores pool commands. It only updates bullet times and culls bullets after some time.
    /// </summary>
    private class DummySoftcullSBC : NoCollSBC {
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

        public override void UpdateVelocityAndControls(bool forceBucketing=false) {
            for (int ii = 0; ii < temp_last; ++ii) {
                if (!Deleted[ii]) {
                    ref SimpleBullet sbn = ref Data[ii];
                    sbn.bpi.t += ETime.FRAME_TIME;
                    if (sbn.bpi.t > ttl) {
                        DeleteSB(ii);
                    }
                }
            }
        }
    }
    private class CircleSBC : SimpleBulletCollection {
        public CircleSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override CollisionResult CheckGrazeCollision(in Hurtbox hurtbox, in SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnCircle(in hurtbox, in sb.bpi.loc.x, in sb.bpi.loc.y, in ColliderGeneric.radius, in sb.scale);
    }
    private class RectSBC : SimpleBulletCollection {
        public RectSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override CollisionResult CheckGrazeCollision(in Hurtbox hurtbox, in SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRect(in hurtbox, in sb.bpi.loc.x, in sb.bpi.loc.y, in ColliderGeneric.halfRect, in ColliderGeneric.maxDist2, in sb.scale, in sb.direction);
    }
    private class LineSBC : SimpleBulletCollection {
        public LineSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override CollisionResult CheckGrazeCollision(in Hurtbox hurtbox, in SimpleBullet sb) => 
            CollisionMath.GrazeCircleOnRotatedSegment(in hurtbox, in sb.bpi.loc.x, in sb.bpi.loc.y, in ColliderGeneric.radius, in ColliderGeneric.linePt1, 
                in ColliderGeneric.delta, in sb.scale, in ColliderGeneric.deltaMag2, in ColliderGeneric.maxDist2, in sb.direction);
    }
    
    public class NoCollSBC : SimpleBulletCollection {
        public NoCollSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}

        public override Maybe<FrameBucketing>? GetCollisionFormat() => default(Maybe<FrameBucketing>?);
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
        void LogFV<T>() {
            //Logs.Log($"FV<{typeof(T).SimpRName()} rent {FrameVars<T>.rented} return {FrameVars<T>.returned}");
        }
        LogFV<float>();
        LogFV<Vector2>();
        Logs.Log($"EnvFrame: Created {EnvFrame.Created} / Cloned {EnvFrame.Cloned} / Disposed {EnvFrame.Disposed}");
        Logs.Log($"PICustomData: Alloc {PIData.Allocated} / Popped {PIData.Popped} / Cached {PIData.Recached} / Copied {PIData.Copied} / Cleared {PIData.Cleared}");
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
        Logs.Log($"Custom pools: {string.Join(", ", activeNpc.Where(x => x.IsCopy).Select(x => x.Style))}");
        Logs.Log($"Empty pools: {string.Join(", ", activeEmpty.Select(x => x.Style))}");
        Logs.Log($"Fancy bullets: {Bullet.NumBullets}", level: LogLevel.INFO);
        Logs.Log($"Total: {total}", level: LogLevel.INFO);
    }
    #endif
}

}