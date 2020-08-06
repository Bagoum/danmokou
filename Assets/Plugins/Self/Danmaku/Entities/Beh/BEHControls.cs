using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using DMath;
using JetBrains.Annotations;

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

        public BEHControl(BehCF act, Pred persistent) {
            action = act;
            persist = persistent;
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
    //Warning: these commands MUST be destroyed in the scope in which they are created, otherwise you will get cT disposal errors.
    public static void ControlPoolSM(Pred persist, BulletManager.StyleSelector styles, SM.StateMachine sm, CancellationToken cT, Pred condFunc) {
        BEHControl pc = new BEHControl(b => {
            if (condFunc(b.rBPI)) {
                _ = BEHPooler.INode(b.rBPI.loc, V2RV2.Angle(b.original_angle), b.GetGlobalDirection(), 
                    b.rBPI.index, null, "f-pool-triggered").RunExternalSM(SMRunner.Cull(sm, cT));
            }
        }, persist);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            AddControlAtEOF(styles.Complex[ii], pc);
        }
    }

    //This is done at end-of-frame to ensure that temporary controls are seen by every bullet before being pruned
    private static void AddControlAtEOF(string style, BEHControl pc) {
        ETime.QueueEOFInvoke(() => LazyGetControls(style).Add(pc));
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
        public static BehCF Time(float time, Pred cond) {
            return b => {
                if (cond(b.rBPI)) b.SetTime(time);
            };
        }
        /// <summary>
        /// Change the style of bullets.
        /// Note: should only be used between the same type, eg pather->pather, laser->laser. Otherwise, weird shit might happen.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF Restyle(string target, Pred cond) {
            FrameAnimBullet.Recolor r = BulletManager.GetRecolor(target);
            return b => {
                if (cond(b.rBPI)) {
                    ((FrameAnimBullet) b).ColorizeOverwrite(r);
                    b.UpdateStyleInformation();
                }
            };
        }
        /// <summary>
        /// Change the bullets into a softcull-type bullet rather than destroying them directly.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF Softcull(string target, Pred cond) {
            return b => {
                if (cond(b.rBPI)) {
                    b.SpawnSimple(target);
                    b.InvokeCull();
                }
            };
        }
        /// <summary>
        /// Run a spawn effect on objects.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF Effect(string target, Pred cond) => b => {
            if (cond(b.rBPI)) b.SpawnSimple(target);
        };
        
        /// <summary>
        /// Destroy bullets.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF Cull(Pred cond) {
            return b => {
                if (cond(b.rBPI)) b.InvokeCull();
            };
        }
        
        /// <summary>
        /// Flip the X-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipX(Pred cond) {
            return b => {
                if (cond(b.rBPI)) b.FlipVelX();
            };
        }
        
        /// <summary>
        /// Flip the Y-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipY(Pred cond) {
            return b => {
                if (cond(b.rBPI)) b.FlipVelY();
            };
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipXGT(BPY wall, Pred cond) {
            return b => {
                var bpi = b.rBPI;
                if (bpi.loc.x > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            };
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the left.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipXLT(BPY wall, Pred cond) {
            return b => {
                var bpi = b.rBPI;
                if (bpi.loc.x < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            };
        }
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the top.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipYGT(BPY wall, Pred cond) {
            return b => {
                var bpi = b.rBPI;
                if (bpi.loc.y > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            };
        }
        
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the bottom.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF FlipYLT(BPY wall, Pred cond) {
            return b => {
                var bpi = b.rBPI;
                if (bpi.loc.y < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            };
        }
        /// <summary>
        /// Add to the x-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF DX(float by, Pred cond) {
            return b => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.x += by;
                }
            };
        }
        /// <summary>
        /// Add to the y-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF DY(float by, Pred cond) {
            return b => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.y += by;
                }
            };
        }
        /// <summary>
        /// Add to the time of objects.
        /// </summary>
        /// <param name="by">Delta time</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF DT(float by, Pred cond) {
            return b => {
                if (cond(b.rBPI)) {
                    b.SetTime(b.rBPI.t + by);
                }
            };
        }
        /// <summary>
        /// Create a sound effect.
        /// </summary>
        /// <param name="sfx">Sound effect</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static BehCF SFX(string sfx, Pred cond) {
            return b => {
                if (cond(b.rBPI)) SFXService.Request(sfx);
            };
        }

        /// <summary>
        /// Freeze an object. It will still collide but it will not move.
        /// </summary>
        public static BehCF Freeze(Pred cond) => b => {
            if (cond(b.rBPI)) b.nextUpdateAllowed = false;
        };

        public static BehCF Batch(Pred cond, BehCF[] over) => b => {
            if (cond(b.rBPI)) {
                for (int ii = 0; ii < over.Length; ++ii) over[ii](b);
            }
        };
    }
    public static void ControlPool(Pred persist, BulletManager.StyleSelector styles, BehCF control) {
        BEHControl pc = new BEHControl(control, persist);
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
            var _pc = new BEHControl(b => b.UpdateStyleCullable(), BulletManager.Consts.NOTPERSISTENT);
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