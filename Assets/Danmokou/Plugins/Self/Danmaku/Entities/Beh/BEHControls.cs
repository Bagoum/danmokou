using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DMath;
using JetBrains.Annotations;
using SM;
using BM = Danmaku.BulletManager;

namespace Danmaku {

public delegate void BehCF(BehaviorEntity beh);
public delegate void BehPF(string pool);
public partial class BehaviorEntity {
    /// <summary>
    /// Structure similar to SimpleBulletCollection, but does not contain its component objects.
    /// </summary>
    public class BEHStyleMetadata {
        public readonly string style;
        public readonly BulletManager.DeferredFramesRecoloring recolor;
        public bool IsPlayer { get; private set; } = false;
        public bool Active { get; private set; } = false;

        public bool CameraCullable { get; set; } = true;

        public BEHStyleMetadata(string style, BulletManager.DeferredFramesRecoloring dfc) {
            this.style = style;
            this.recolor = dfc;
        }
        
        private void SetPlayer() {
            IsPlayer = true;
        }

        public BEHStyleMetadata MakePlayerCopy(string newPool) {
            var bsm = new BEHStyleMetadata(newPool, recolor.MakePlayerCopy());
            bsm.SetPlayer();
            return bsm;
        }
        
        public void ResetPoolMetadata() {
            CameraCullable = true;
        }

        public void Reset() => ResetPoolMetadata();
        
        public void Activate() {
            if (!Active) {
                activePools.Add(this);
                Log.Unity($"Activating beh pool {style}", level: Log.Level.DEBUG1);
                Active = true;
            }
        }

        public void Deactivate() {
            Active = false;
        }

        public void AddPoolControlEOF(BEHControl pc) => 
            ETime.QueueEOFInvoke(() => controls.AddPriority(pc, pc.priority));
        
        public void PruneControls() {
            for (int ii = 0; ii < controls.Count; ++ii) {
                if (controls[ii].cT.Cancelled || !controls[ii].persist(GlobalBEH.Main.rBPI)) {
                    controls.Delete(ii);
                }
            }
            controls.Compact();
        }
        public void ClearControls() => controls.Empty();
        
        private readonly DMCompactingArray<BEHControl> controls = new DMCompactingArray<BEHControl>(4);

        public void IterateControls(BehaviorEntity beh) {
            int ct = controls.Count;
            for (int ii = 0; ii < ct && !beh.dying; ++ii) {
                controls[ii].action(beh);
            }
        }
    }
    
    private static readonly BEHStyleMetadata defaultMeta = new BEHStyleMetadata(null, null);
    
    /// <summary>
    /// Complex bullet pool control descriptor.
    /// </summary>
    public readonly struct BEHControl {
        public readonly BehCF action;
        public readonly Pred persist;
        public readonly int priority;
        public readonly ICancellee cT;

        public BEHControl(BehCFc act, Pred persistent, [CanBeNull] ICancellee cT = null) {
            action = act.Func(this.cT = cT ?? Cancellable.Null);
            persist = persistent;
            priority = act.priority;
        }
        public BEHControl(BehCF act, Pred persistent, int priorty, ICancellee cT) {
            action = act;
            persist = persistent;
            priority = priorty;
            this.cT = cT;
        }
    }
    /// <summary>
    /// Pool definitions for bullet styles that are active. Pools are deactivated on each scene and activated when used.
    /// </summary>
    private static readonly List<BEHStyleMetadata> activePools = new List<BEHStyleMetadata>(16);

    public static BEHStyleMetadata GetPool(string key) {
        if (BulletManager.CheckComplexPool(key, out var pool)) return pool;
        throw new Exception($"No BEH style by name {key}");
    }
    public static void DeInitializePools() {
        foreach (var x in activePools) {
            x.Reset();
            x.Deactivate();
        }
        activePools.Clear();
    }
    private static readonly HashSet<string> ignoreCullStyles = new HashSet<string>();

    //set by initialize > updatestyleinfo
    public BEHStyleMetadata myStyle { get; private set; }

    protected virtual void UpdateStyle(BEHStyleMetadata newStyle) {
        myStyle = newStyle;
    }

    /// <summary>
    /// DEPRECATED
    /// </summary>
    public static void ControlPoolSM(Pred persist, BulletManager.StyleSelector styles, SM.StateMachine sm, ICancellee cT, Pred condFunc) {
        BEHControl pc = new BEHControl(b => {
            if (condFunc(b.rBPI)) {
                using (var gcx = PrivateDataHoisting.GetGCX(b.rBPI.id)) {
                    _ = b.GetINode("f-pool-triggered", null).RunExternalSM(SMRunner.Cull(sm, cT, gcx));
                }
            }
        }, persist, BulletManager.BulletControl.P_RUN, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            GetPool(styles.Complex[ii]).AddPoolControlEOF(pc);
        }
    }

    /// <summary>
    /// Repository for functions that can be applied to BehaviorEntities via the `beh-control` command,
    /// including complex bullets such as lasers and pathers.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static class BulletControls {
        /// <summary>
        /// Set the time of bullets.
        /// </summary>
        /// <param name="time">Time to set</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc Time(float time, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) b.SetTime(time);
            }, BM.BulletControl.P_MOVE_1);
        }
        /// <summary>
        /// Change the style of bullets.
        /// Note: should only be used between the same type, eg pather->pather, laser->laser. Otherwise, weird shit might happen.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc Restyle(string target, Pred cond) {
            var style = GetPool(target);
            FrameAnimBullet.Recolor r = style.recolor.GetOrLoadRecolor();
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    ((Bullet)b).ColorizeOverwrite(r);
                    b.UpdateStyle(style);
                }
            }, BM.BulletControl.P_CULL);
        }
        /// <summary>
        /// Change the bullets into a softcull-type bullet rather than destroying them directly.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc Softcull(string target, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    b.SpawnSimple(target);
                    b.InvokeCull();
                }
            }, BM.BulletControl.P_CULL);
        }
        /// <summary>
        /// Run a spawn effect on objects.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc Effect(string target, Pred cond) => new BehCFc(b => {
            if (cond(b.rBPI)) b.SpawnSimple(target);
        }, BM.BulletControl.P_RUN);
        
        /// <summary>
        /// Destroy bullets.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc Cull(Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) b.InvokeCull();
            }, BM.BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Flip the X-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipX(Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) b.FlipVelX();
            }, BM.BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the Y-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipY(Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) b.FlipVelY();
            }, BM.BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipXGT(BPY wall, Pred cond) {
            return new BehCFc(b => {
                var bpi = b.rBPI;
                if (bpi.loc.x > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            }, BM.BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the left.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipXLT(BPY wall, Pred cond) {
            return new BehCFc(b => {
                var bpi = b.rBPI;
                if (bpi.loc.x < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            }, BM.BulletControl.P_MOVE_3);
        }
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the top.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipYGT(BPY wall, Pred cond) {
            return new BehCFc(b => {
                var bpi = b.rBPI;
                if (bpi.loc.y > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            }, BM.BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the bottom.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc FlipYLT(BPY wall, Pred cond) {
            return new BehCFc(b => {
                var bpi = b.rBPI;
                if (bpi.loc.y < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            }, BM.BulletControl.P_MOVE_3);
        }
        /// <summary>
        /// Add to the x-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc DX(float by, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.x += by;
                }
            }, BM.BulletControl.P_MOVE_2);
        }
        /// <summary>
        /// Add to the y-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc DY(float by, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.y += by;
                }
            }, BM.BulletControl.P_MOVE_2);
        }
        /// <summary>
        /// Add to the time of objects.
        /// </summary>
        /// <param name="by">Delta time</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc DT(float by, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    b.SetTime(b.rBPI.t + by);
                }
            }, BM.BulletControl.P_MOVE_1);
        }
        /// <summary>
        /// Create a sound effect.
        /// </summary>
        /// <param name="sfx">Sound effect</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCFc SFX(string sfx, Pred cond) {
            return new BehCFc(b => {
                if (cond(b.rBPI)) SFXService.Request(sfx);
            }, BM.BulletControl.P_RUN);
        }

        /// <summary>
        /// Freeze an object. It will still collide but it will not move.
        /// </summary>
        public static BehCFc Freeze(Pred cond) => new BehCFc(b => {
            if (cond(b.rBPI)) b.nextUpdateAllowed = false;
        }, BM.BulletControl.P_TIMECONTROL);

        public static BehCFc UpdateF((string target, BPY valuer)[] targets, Pred cond) {
            var ftargets = targets.Select(t => (PrivateDataHoisting.GetKey(t.target), t.valuer)).ToArray();
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    var bpi = b.rBPI;
                    for (int ii = 0; ii < ftargets.Length; ++ii) {
                        PrivateDataHoisting.UpdateValue(bpi.id, ftargets[ii].Item1, ftargets[ii].valuer(bpi));
                    }
                }
            }, BM.BulletControl.P_SAVE);
        }
        public static BehCFc UpdateV2((string target, TP valuer)[] targets, Pred cond) {
            var ftargets = targets.Select(t => (PrivateDataHoisting.GetKey(t.target), t.valuer)).ToArray();
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    var bpi = b.rBPI;
                    for (int ii = 0; ii < ftargets.Length; ++ii) {
                        PrivateDataHoisting.UpdateValue(bpi.id, ftargets[ii].Item1, ftargets[ii].valuer(bpi));
                    }
                }
            }, BM.BulletControl.P_SAVE);
        }

        /// <summary>
        /// Batch several commands together under one predicate.
        /// </summary>
        public static BehCFc Batch(Pred cond, BehCFc[] over) {
            var priority = over.Max(o => o.priority);
            return new BehCFc(ct => {
                var funcs = over.Select(o => o.Func(ct)).ToArray();
                return b => {
                    if (cond(b.rBPI)) {
                        for (int ii = 0; ii < over.Length; ++ii) funcs[ii](b);
                    } };
            }, priority);
        }

        
        /// <summary>
        /// If the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        public static BehCFc SM(Pred cond, StateMachine target) => new BehCFc(cT => b => {
            if (cond(b.rBPI)) {
                using (var gcx = PrivateDataHoisting.GetGCX(b.rBPI.id)) {
                    _ = b.GetINode("f-pool-triggered", null).RunExternalSM(SMRunner.Cull(target, cT, gcx));
                }
            }
        }, BulletManager.BulletControl.P_RUN);
    }
    
    public static void ControlPool(Pred persist, BM.StyleSelector styles, BehCFc control, ICancellee cT) {
        BEHControl pc = new BEHControl(control, persist, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            GetPool(styles.Complex[ii]).AddPoolControlEOF(pc);
        }
    }

    /// <summary>
    /// Repository for functions that can be applied to BehaviorEntities via the `behpool-control` command,
    /// including complex bullets such as lasers and pathers.
    /// These functions are applied to the metadata applied to each BehaviorEntity style,
    /// rather than the objects themselves.
    /// </summary>
    public static class PoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// </summary>
        /// <returns></returns>
        public static BehPF Reset() {
            return pool => GetPool(pool).ClearControls();
        }

        /// <summary>
        /// Set whether or not a pool can cull bullets that are out of camera range.
        /// </summary>
        /// <param name="cullActive">True iff camera culling is allowed.</param>
        /// <returns></returns>
        public static BehPF AllowCull(bool cullActive) {
            return pool => GetPool(pool).CameraCullable = cullActive;
        }

        /// <summary>
        /// Unconditionally softcull all bullets in a pool with an automatically-determined cull style.
        /// </summary>
        /// <param name="targetFormat">Base cull style, eg. cwheel</param>
        /// <returns></returns>
        public static BehPF SoftCullAll(string targetFormat) {
            return pool => GetPool(pool).AddPoolControlEOF(new BEHControl(
                BulletControls.Softcull(BulletManager.PortColorFormat(pool, targetFormat, "red/w"), 
                _ => true), BulletManager.Consts.NOTPERSISTENT));
        }
    }
    
    public static void ControlPool(BulletManager.StyleSelector styles, BehPF control) {
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            control(styles.Complex[ii]);
        }
    }

    /// <param name="targetFormat">Base cull style, eg. 'cwheel'</param>
    /// <param name="defaulter">Default color if no match is found, eg. 'red/'</param>
    /// <param name="cullPools">List of pools to cull</param>
    public static void Autocull(string targetFormat, string defaulter, [CanBeNull] string[] cullPools = null) {
        void CullPool(string poolStr) {
            if (!BulletManager.CheckComplexPool(poolStr, out var pool) || pool.IsPlayer) return;
            if (!BulletManager.PortColorFormat(poolStr, targetFormat, defaulter, out string target)) return;
            pool.AddPoolControlEOF(new BEHControl(
                BulletControls.Softcull(target, _ => true), BulletManager.Consts.NOTPERSISTENT));
        }
        foreach (var pool in (cullPools ?? activePools.Select(x => x.style))) CullPool(pool);
    }

    public static void PrunePoolControls() {
        for (int ii = 0; ii < activePools.Count; ++ii) {
            activePools[ii].PruneControls();
        }
    }

    public static void ClearPoolControls(bool clearPlayer) {
        for (int ii = 0; ii < activePools.Count; ++ii) {
            if (clearPlayer || !activePools[ii].IsPlayer) activePools[ii].ClearControls();
        }
    }
}
}