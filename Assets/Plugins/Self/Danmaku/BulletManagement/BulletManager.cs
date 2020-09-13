using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Rendering;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Threading;
using DMath;
using Core;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Profiling;
using Collision = DMath.Collision;
using ExSBCF = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx>;

namespace Danmaku {

public readonly struct SBCFp {
    public readonly ExSBCF func;
    public readonly int priority;
    public readonly Func<ICancellee, SBCF> lazyFunc;
    public SBCFp(ExSBCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }

    public SBCFp(Func<ICancellee, SBCF> lazy, int p) {
        func = null;
        priority = p;
        lazyFunc = lazy;
    }
}
public readonly struct SBCFc {
    private readonly SBCF func;
    private readonly Func<ICancellee, SBCF> lazyFunc;
    public readonly int priority;
    public SBCFc(SBCFp p) {
        func = p.func == null ? null : Compilers.SBCF(p.func);
        priority = p.priority;
        lazyFunc = p.lazyFunc;
    }

    public SBCF Func(ICancellee cT) => func ?? lazyFunc(cT);

    private SBCFc(SBCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }

    public static SBCFc Manual(SBCF f, int p) => new SBCFc(f, p);
}
//Not compiled but using this for priority and lazy alternates
public readonly struct BehCFc {
    private readonly BehCF func;
    private readonly Func<ICancellee, BehCF> lazyFunc;
    public readonly int priority;

    public BehCFc(BehCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }
    public BehCFc(Func<ICancellee, BehCF> f, int p) {
        func = null;
        priority = p;
        lazyFunc = f;
    }
    
    public BehCF Func(ICancellee cT) => func ?? lazyFunc(cT);
    
}

public partial class BulletManager {
    public const string EMPTY = "empty";

    public static void CopyPool(string newPool, string from) {
        var p = simpleBulletPools[from].CopyPool((from == "empty") ? activeCEmpty : activeCNpc, newPool);
        AddSimpleStyle(newPool, p);
        p.Activate();
    }

    private static readonly Dictionary<string, string> playerPoolCopyCache = new Dictionary<string, string>();
    public static string GetOrMakePlayerCopy(string pool) {
        //lmao i hate garbage
        if (!playerPoolCopyCache.TryGetValue(pool, out var playerPool)) {
            playerPool = playerPoolCopyCache[pool] = $"p-{pool}";
        }
        if (!simpleBulletPools.ContainsKey(playerPool)) {
            var p = simpleBulletPools[pool].CopySimplePool(playerStyles, playerPool);
            AddSimpleStyle(playerPool, p);
            p.SetPlayer();
            p.Activate();
        }
        return playerPool;
    }

    private static bool CheckOrCopyPool(string pool, out SimpleBulletCollection sbc) {
        if (simpleBulletPools.TryGetValue(pool, out sbc)) {
            if (!sbc.Active) sbc.Activate();
            return true;
        }
        int splitAt = pool.IndexOf('.');
        if (splitAt == -1) return false;
        string basePool = pool.Substring(0, splitAt);
        if (!simpleBulletPools.ContainsKey(basePool)) return false;
        CopyPool(pool, basePool);
        sbc = simpleBulletPools[pool];
        return true;
    }

    public static bool PoolExists(string pool) => simpleBulletPools.ContainsKey(pool);

    public static void AssertControls(string pool, IReadOnlyList<BulletControl> controls) => GetMaybeCopyPool(pool).AssertControls(controls);

    private static SimpleBulletCollection GetMaybeCopyPool(string pool) {
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }
    private static readonly ExFunction getMaybeCopyPool = ExUtils.Wrap<string>(typeof(BulletManager), "GetMaybeCopyPool");

    public override int UpdatePriority => UpdatePriorities.BM;
    public override void RegularUpdate() {
        SimpleBulletCollection sbc;
        //Temp-last set for control updates
        for (int ii = 0; ii< activeCEmpty.Count; ++ii) {
            sbc = activeCEmpty[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii< activeNpc.Count; ++ii) {
            sbc = activeNpc[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii<activeCNpc.Count; ++ii) {
            sbc = activeCNpc[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii < playerStyles.Count; ++ii) {
            sbc = playerStyles[ii];
            sbc.temp_last = sbc.Count;
        }
        //Velocity and control updates
        for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
            sbc = activeCEmpty[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        Profiler.BeginSample("NPC-fired simple bullet velocity updates");
        for (int ii = 0; ii < activeNpc.Count; ++ii) {
            sbc = activeNpc[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        Profiler.EndSample();
        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            sbc = activeCNpc[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        for (int ii = 0; ii < playerStyles.Count; ++ii) {
            sbc = playerStyles[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        Profiler.BeginSample("NPC-fired simple bullet collision checking");
        int dmg = 0; int graze = 0;
        for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
            sbc = activeCEmpty[ii];
            if (sbc.Count > 0) {
                CollisionCheckResults ccr = sbc.CheckCollision();
                dmg = Math.Max(dmg, ccr.damage);
                graze += ccr.graze;
            }
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) {
            sbc = activeNpc[ii];
            if (sbc.Count > 0) {
                CollisionCheckResults ccr = sbc.CheckCollision();
                dmg = Math.Max(dmg, ccr.damage);
                graze += ccr.graze;
            }
        }
        Profiler.EndSample();
        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            sbc = activeCNpc[ii];
            if (sbc.Count > 0) {
                CollisionCheckResults ccr = sbc.CheckCollision();
                dmg = Math.Max(dmg, ccr.damage);
                graze += ccr.graze;
            }
        }
        if (dmg > 0) {
            Events.TryHitPlayer.Invoke((dmg, false));
        }
        if (graze > 0) {
            GameManagement.campaign.AddGraze(graze);
        }
        //Collision check (player bullets)
        var fci = Enemy.FrozenEnemies;
        for (int ii = 0; ii < playerStyles.Count; ++ii) {
            sbc = playerStyles[ii];
            if (sbc.Count > 0) sbc.CheckCollision(fci);
        }
        
    }

    public static void ExternalBulletProc(int dmg, int graze) {
        if (dmg > 0) {
            Events.TryHitPlayer.Invoke((dmg, false));
        }
        if (graze > 0) {
            GameManagement.campaign.AddGraze(graze);
        }
    }

    private void StartScene() {
        simpleBulletPools[EMPTY].Activate();
        CreateBulletContainer();
    }
    private void CreateBulletContainer() {
        GameObject go = new GameObject {name = "Bullet Spam Container"};
        spamContainer = go.transform;
        spamContainer.position = Vector3.zero;
    }

    public static void OrphanAll() {
        ClearPoolControls();
        foreach (var pool in simpleBulletPools.Values) {
            pool.Reset();
            pool.Deactivate();
        }
        DestroyCopiedPools();
        activeNpc.Clear();
        playerStyles.Clear();
        Bullet.OrphanAll();
        BehaviorEntity.DeInitializePools();
    }

    public static void DestroyCopiedPools() {
        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            DestroySimpleStyle(activeCNpc[ii].Style);
        }
        activeCNpc.Clear();
        for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
            DestroySimpleStyle(activeCEmpty[ii].Style);
        }
        activeCEmpty.Clear();
    }

    public static void ClearEmpty() => simpleBulletPools[EMPTY].Reset();

    public static void ClearAllBullets() {
        foreach (string key in simpleBulletPools.Keys) {
            simpleBulletPools[key].Reset();
        }
        ClearNonSimpleBullets();
    }

    public static void ClearNonSimpleBullets() {
        Bullet.ClearAll();
    }
    public static void ClearPoolControls() {
        foreach (var pool in simpleBulletPools.Values) {
            pool.ClearPoolControl();
            pool.ResetPoolMetadata();
        }
        BehaviorEntity.ClearControls();
        CurvedTileRenderLaser.ClearControls();
    }
}

public partial class BulletManager {
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
        public Velocity velocity; //Don't make this readonly
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

        public SimpleBullet([CanBeNull] BPY scaleF, [CanBeNull] SBV2 dirF, Velocity velocity, int firingIndex, uint id, float timeOffset) {
            scaleFunc = scaleF;
            dirFunc = dirF;
            scale = 1f;
            this.velocity = velocity;
            grazeFrameCounter = cullFrameCounter = 0;
            this.accDelta = Vector2.zero;
            bpi = new ParametricInfo(velocity.rootPos, firingIndex, id, timeOffset);
            direction = this.velocity.UpdateZero(ref bpi, timeOffset);
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
        public abstract void ClearPoolControl();
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
                Log.Unity($"Activating pool {Style}", level: Log.Level.DEBUG1);
                Active = true;
            }
        }

        public void Deactivate() {
            Active = false;
            temp_last = 0;
        }

        public override string Style => bc.name;
        protected BulletInCode bc;

        public void SetCullRad(float r) => bc.CULL_RAD = r;
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
            return BEHPooler.INode(sb.bpi.loc, V2RV2.Angle(sb.velocity.angle), 
                sb.direction, sb.bpi.index, bpiid, behName);
        }

        public virtual CollectionType MetaType => CollectionType.Normal;

        public bool Deletable => bc.deletable;

        [UsedImplicitly]
        public virtual void AppendSoftcull(AbsSimpleBulletCollection sbc, int ii) {
            throw new NotImplementedException("Cannot softcull to non-cull style " + Style);
        }
        public static readonly ExFunction appendSoftcull = ExUtils.Wrap<SimpleBulletCollection>("AppendSoftcull", new[] {typeof(AbsSimpleBulletCollection), typeof(int)});
        protected virtual CollisionResult CheckGrazeCollision(ref SimpleBullet sb) => throw new NotImplementedException();

        public override void ClearPoolControl() => pcs.Empty();
        public void ResetPoolMetadata() {
            bc.ResetMetadata();
            allowCameraCull = true;
        }

        public void AddPoolControl(BulletControl pc) => pcs.AddPriority(pc, pc.priority);
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

        public void PruneControls() {
            for (int ii = 0; ii < pcs.Count; ++ii) {
                if (!pcs[ii].persist(GlobalBEH.Main.rBPI)) {
                    pcs.Delete(ii);
                }
            }
            pcs.Compact();
        }

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
                sb.velocity.UpdateDeltaAssignAcc(ref sb.bpi, out sb.accDelta, in nextDT);
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

        public virtual CollisionCheckResults CheckCollision() {
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
                    CollisionResult cr = CheckGrazeCollision(ref sbn);
                    if (cr.collide) {
                        collisionDamage = bc.damageAgainstPlayer;
                        if (bc.destructible) Delete(ii, true);
                    } else if (checkGraze && cr.graze) {
                        sbn.grazeFrameCounter = bc.grazeEveryFrames;
                        ++graze;
                    } else if (allowCameraCull && (++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationService.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        Delete(ii, true);
                    }
                }
            }
            Compact();
            return new CollisionCheckResults(collisionDamage, graze);
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
                    if ((++sbn.cullFrameCounter & CULL_EVERY_MASK) == 0 && LocationService.OffPlayableScreenBy(bc.CULL_RAD, sbn.bpi.loc)) {
                        PlayerFireDataHoisting.Delete(sbn.bpi.id);
                        Delete(ii, true);
                    } else {
                        for (int ff = 0; ff < fciL; ++ff) {
                            if (fci[ff].Active && 
                                Collision.CircleOnCircle(fci[ff].pos, fci[ff].radius, sbn.bpi.loc, bc.cc.effRadius)) {
                                //Stage enemies don't absorb bullets if they're invulnerable
                                if (!fci[ff].enemy.takesBossDamage && !fci[ff].enemy.Vulnerable) continue;
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

        public override CollisionCheckResults CheckCollision() {
            Compact();
            return new CollisionCheckResults(0, 0);
        }
    }
    private class CircleSBC : SimpleBulletCollection {
        public CircleSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}

        protected override CollisionResult CheckGrazeCollision(ref SimpleBullet sb) => 
            Collision.GrazeCircleOnCircle(bc.collisionTarget, sb.bpi.loc, bc.cc.radius * sb.scale);
    }
    private class RectSBC : SimpleBulletCollection {
        public RectSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(ref SimpleBullet sb) => 
            Collision.GrazeCircleOnRect(bc.collisionTarget, sb.bpi.loc, bc.cc.halfRect.x, 
                bc.cc.halfRect.y, bc.cc.maxDist2, sb.scale, sb.direction.x, sb.direction.y);
    }
    private class LineSBC : SimpleBulletCollection {
        public LineSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(ref SimpleBullet sb) => 
            Collision.GrazeCircleOnRotatedSegment(bc.collisionTarget, sb.bpi.loc, bc.cc.radius, bc.cc.linePt1, 
                bc.cc.delta, sb.scale, bc.cc.deltaMag2, bc.cc.maxDist2, sb.direction.x, sb.direction.y);
    }
    private class NoCollSBC : SimpleBulletCollection {
        public NoCollSBC(List<SimpleBulletCollection> target, BulletInCode bc) : base(target, bc) {}
        protected override CollisionResult CheckGrazeCollision(ref SimpleBullet sb) => CollisionResult.noColl;
    }

    private bool playerRendered;
    private bool enemyRendered;
    //Called via Camera.onPreCull event
    private void RenderBullets(Camera c) {
        if (!Application.isPlaying) { return; }
        if (playerRendered && enemyRendered) {
            playerRendered = enemyRendered = false;
        }
        SimpleBulletCollection sbc;
        if ((c.cullingMask & ppLayerMask) != 0) {
            for (int ii = 0; ii < playerStyles.Count; ++ii) {
                sbc = playerStyles[ii];
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
    }

    private void SwitchRenderPool(Camera c, SimpleBulletCollection pool, int layer) {
        if (SaveData.s.LegacyRenderer) LegacyRenderPool(c, pool, layer);
        else RenderPool(c, pool, layer);
    }
    private void LegacyRenderPool(Camera c, SimpleBulletCollection pool, int layer) {
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
                timeArr[ib] = sb.bpi.t;
            }
            pb.SetFloatArray(timePropertyId, timeArr);
            CallLegacyRender(ri, c, layer, run);
        }
    }
    private void RenderPool(Camera c, SimpleBulletCollection pool, int layer) {
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
                //Note that you can't flip objects with float2. The standard 2x2 rot is
                //x -y
                //y  x
                //For flip x, you need to make the first column negative; for flip y the second column.
                //If you're using Seija, then camera flipping takes care of object flipping. 
                timeArr[ib] = sb.bpi.t;
            }
            pb.SetVectorArray(posDirPropertyId, posDirArr);
            pb.SetFloatArray(timePropertyId, timeArr);
            CallRender(ri, c, layer, run);
        }
    }

    private void CallLegacyRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        Graphics.DrawMeshInstanced(ri.mesh, 0, ri.material,
            matArr,
            count: ct,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layer,
            camera: c);
    }
    private void CallRender(MeshGenerator.RenderInfo ri, Camera c, int layer, int ct) {
        Graphics.DrawMeshInstancedProcedural(ri.mesh, 0, ri.material,
          bounds: drawBounds,
          count: ct,
          properties: pb,
          castShadows: ShadowCastingMode.Off,
          receiveShadows: false,
          layer: layer,
          camera: c);
    }

    private unsafe void PrepareRendering() {
        Debug.Log($"BS size: {Marshal.SizeOf(typeof(SimpleBullet))}, VelStruct size: {Marshal.SizeOf(typeof(Velocity))}");
        Debug.Log($"BPI size: {Marshal.SizeOf(typeof(ParametricInfo))}, CollInfo side: {Marshal.SizeOf(typeof(CollisionResult))}");
        Debug.Log($"Float size: {sizeof(float)} (4), Long size: {sizeof(long)} (8), V2 size: {sizeof(Vector2)} (8)");
    }

    private const int batchSize = 1023; //duplicated in BulletIndirect array lens
    private MaterialPropertyBlock pb;
    private static readonly int posDirPropertyId = Shader.PropertyToID("posDirBuffer");
    private static readonly int timePropertyId = Shader.PropertyToID("timeBuffer");
    private static readonly Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private readonly Vector4[] posDirArr = new Vector4[batchSize];
    private readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private readonly float[] timeArr = new float[batchSize];

    private void OnDestroy() {
        ScriptableObject.Destroy(throwaway_gm);
    }

    public static FrameAnimBullet.Recolor GetRecolor(string fabName) => bulletStyles[fabName].GetOrLoadRecolor();
    
    #if UNITY_EDITOR
    [ContextMenu("Debug bullet numbers")]
    public void DebugBulletNums() {
        int total = 0;
        foreach (var pool in simpleBulletPools.Values) {
            total += pool.Count;
            if (pool.Count > 0) Log.Unity($"{pool.Style}: {pool.Count}", level: Log.Level.INFO);
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