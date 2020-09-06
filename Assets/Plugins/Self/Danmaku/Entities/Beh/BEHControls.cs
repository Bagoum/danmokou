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
    /// Complex bullet pool control descriptor.
    /// </summary>
    private readonly struct BEHControl {
        public readonly BehCF action;
        public readonly Pred persist;
        public readonly int priority;

        public BEHControl(BehCFc act, Pred persistent, CancellationToken? cT = null) {
            action = act.Func(cT ?? CancellationToken.None);
            persist = persistent;
            priority = act.priority;
        }
        public BEHControl(BehCF act, Pred persistent, int priorty) {
            action = act;
            persist = persistent;
            priority = priorty;
        }
    }
    /// <summary>
    /// WARNING: Do NOT add controls directly to this array. Use AddControlAtEOF instead.
    /// Pool controls for complex bullets. Keys are added the first time a command is created or a bullet is spawned.
    /// Non-persistent pruning is handled by PruneControls, which is invoked by GameManagement at end of frame.
    /// </summary>
    private static readonly Dictionary<string, DMCompactingArray<BEHControl>> controls = new Dictionary<string, DMCompactingArray<BEHControl>>();
    /// <summary>
    /// Same as controls with list iteration.
    /// </summary>
    private static readonly List<DMCompactingArray<BEHControl>> initializedPools = new List<DMCompactingArray<BEHControl>>(16);

    public static void DeInitializePools() {
        controls.Clear();
        foreach (var x in initializedPools) x.Empty();
        initializedPools.Clear();
    }
    private static readonly HashSet<string> ignoreCullStyles = new HashSet<string>();

    protected string style = "defaultNoStyle";
    protected bool styleIsCameraCullable = true;
    //set by initialize > updatestyleinfo
    private DMCompactingArray<BEHControl> thisStyleControls;
    protected virtual void UpdateStyleCullable() {
        styleIsCameraCullable = !ignoreCullStyles.Contains(style);
    }

    protected virtual void UpdateStyleControls() {
        thisStyleControls = LazyGetControls(style);
    }

    private void UpdateStyleInformation() {
        UpdateStyleCullable();
        UpdateStyleControls();
        //TODO virtualize, allow pather/laser to set PB again
    }

    private static DMCompactingArray<BEHControl> LazyGetControls(string style) {
        if (!controls.ContainsKey(style)) {
            controls[style] = new DMCompactingArray<BEHControl>();
            initializedPools.Add(controls[style]);
        }
        return controls[style];
    }

    /// <summary>
    /// DEPRECATED
    /// </summary>
    public static void ControlPoolSM(Pred persist, BulletManager.StyleSelector styles, SM.StateMachine sm, CancellationToken cT, Pred condFunc) {
        BEHControl pc = new BEHControl(b => {
            if (condFunc(b.rBPI)) {
                using (var gcx = PrivateDataHoisting.GetGCX(b.rBPI.id)) {
                    _ = b.GetINode("f-pool-triggered", null).RunExternalSM(SMRunner.Cull(sm, cT, gcx));
                }
            }
        }, persist, BulletManager.BulletControl.P_RUN);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            AddControlAtEOF(styles.Complex[ii], pc);
        }
    }

    //This is done at end-of-frame to ensure that temporary controls are seen by every bullet before being pruned
    private static void AddControlAtEOF(string style, BEHControl pc) {
        ETime.QueueEOFInvoke(() => LazyGetControls(style).AddPriority(pc, pc.priority));
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
            FrameAnimBullet.Recolor r = BulletManager.GetRecolor(target);
            return new BehCFc(b => {
                if (cond(b.rBPI)) {
                    ((Bullet)b).ColorizeOverwrite(r);
                    b.UpdateStyleInformation();
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
    
    public static void ControlPool(Pred persist, BM.StyleSelector styles, BehCFc control, CancellationToken cT) {
        BEHControl pc = new BEHControl(control, persist, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            AddControlAtEOF(styles.Complex[ii], pc);
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
            return pool => LazyGetControls(pool).Empty();
        }

        /// <summary>
        /// Set whether or not a pool can cull bullets that are out of camera range.
        /// </summary>
        /// <param name="cullActive">True iff camera culling is allowed.</param>
        /// <returns></returns>
        public static BehPF AllowCull(bool cullActive) {
            var _pc = new BEHControl(b => b.UpdateStyleCullable(), BulletManager.Consts.NOTPERSISTENT, 
                BM.BulletControl.P_SETTINGS);
            return pool => {
                if (cullActive) ignoreCullStyles.Remove(pool);
                else ignoreCullStyles.Add(pool);
                AddControlAtEOF(pool, _pc);
            };
        }

        /// <summary>
        /// Unconditionally softcull all bullets in a pool with an automatically-determined cull style.
        /// </summary>
        /// <param name="targetFormat">Base cull style, eg. cwheel</param>
        /// <returns></returns>
        public static BehPF SoftCullAll(string targetFormat) {
            return pool => AddControlAtEOF(pool, new BEHControl(
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
    /// <param name="pools">List of pools to cull</param>
    public static void Autocull(string targetFormat, string defaulter, [CanBeNull] string[] pools = null) {
        void CullPool(string pool) {
            if (!controls.ContainsKey(pool)) return;
            if (pool.StartsWith("p-")) return; //Player bullets
            if (!BulletManager.PortColorFormat(pool, targetFormat, defaulter, out string target)) return;
            AddControlAtEOF(pool, new BEHControl(
                BulletControls.Softcull(target, _ => true), BulletManager.Consts.NOTPERSISTENT));
        }
        foreach (var pool in (pools ?? controls.Keys.ToArray())) CullPool(pool);
    }

    public static void PruneControls() {
        for (int ii = 0; ii < initializedPools.Count; ++ii) {
            var pcs = initializedPools[ii];
            for (int jj = 0; jj < pcs.Count; ++jj) {
                if (!pcs[jj].persist(GlobalBEH.Main.rBPI)) {
                    pcs.Delete(jj);
                } 
            }
            pcs.Compact();
        }
    }

    public static void ClearControls() {
        for (int ii = 0; ii < initializedPools.Count; ++ii) {
            initializedPools[ii].Empty();
        }
    }
}
}