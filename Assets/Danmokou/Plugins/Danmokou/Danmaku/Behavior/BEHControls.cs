using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.DMath;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Danmaku.BulletManager;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;

namespace Danmokou.Behavior {

/// <summary>
/// A control function performing some operation on a behavior entity.
/// <br/>The cancellation token is stored in the BehControl struct. It may be used by the control
/// to bound nested summons (eg. via the SM control).
/// </summary>
public delegate void BehCF(BehaviorEntity beh, ICancellee cT);
/// <summary>
/// A pool control function performing some operation on a behavior entity style.
/// <br/>The operation may be able to be reset by disposing the returned token.
/// </summary>
public delegate IDisposable BehPF(string pool, ICancellee cT);
public partial class BehaviorEntity {
    //No compilation
    public readonly struct cBEHControl {
        public readonly BehCF action;
        public readonly int priority;
        
        public cBEHControl(BehCF action, int priority) {
            this.action = action;
            this.priority = priority;
        }
    }
    
    /// <summary>
    /// Complex bullet pool control descriptor.
    /// </summary>
    public readonly struct BEHControl {
        public readonly BehCF action;
        public readonly GenCtx caller;
        public readonly GCXF<bool> persist;
        public readonly int priority;
        public readonly ICancellee cT;

        public BEHControl(GenCtx caller, cBEHControl act, GCXF<bool> persistent, ICancellee? cT) {
            this.cT = cT ?? Cancellable.Null;
            this.caller = caller;
            action = act.action;
            persist = persistent;
            priority = act.priority;
        }
        public BEHControl Mirror() => new(caller.Mirror(), new(action, priority), persist, cT);
    }

    
    /// <summary>
    /// Structure similar to SimpleBulletCollection, but does not contain its component objects.
    /// </summary>
    public class BEHStyleMetadata {
        public readonly string? style;
        public readonly DeferredFramesRecoloring? recolor;
        public FrameAnimBullet.Recolor RecolorOrThrow => (recolor ??
            throw new Exception($"Couldn't resolve BEHStyleMetadata recolor for {style}")).GetOrLoadRecolor();
        public bool IsPlayer { get; private set; } = false;
        public bool Active { get; private set; } = false;

        public DisturbedEvented<bool,bool> CameraCullable { get; } = new OverrideEvented<bool>(true);

        public BEHStyleMetadata(string? style, DeferredFramesRecoloring? dfc) {
            this.style = style;
            this.recolor = dfc;
        }
        
        private void SetPlayer() {
            IsPlayer = true;
        }

        public BEHStyleMetadata MakePlayerCopy(string newPool) {
            var bsm = new BEHStyleMetadata(newPool, recolor?.MakePlayerCopy());
            bsm.SetPlayer();
            return bsm;
        }

        public void Reset() {
            
        }
        
        public void Activate() {
            if (!Active) {
                activePools.Add(this);
                Logs.Log($"Activating beh pool {style}", level: LogLevel.DEBUG1);
                Active = true;
            }
        }

        public void Deactivate() {
            Active = false;
        }

        public void AddBulletControlEOF(BEHControl pc) => 
            ETime.QueueEOFInvoke(() => (pc.priority switch {
                BulletControl.P_ON_COLLIDE => onCollideControls,
                BulletControl.P_ON_DESTROY => onDestroyControls,
                _ => controls
            }).AddPriority(pc, pc.priority));
        
        public void PruneControls() {
            void Prune(DMCompactingArray<BEHControl> carr) {
                for (int ii = 0; ii < carr.Count; ++ii) {
                    if (carr[ii].cT.Cancelled || !carr[ii].persist(carr[ii].caller)) {
                        carr[ii].caller.Dispose();
                        carr.Delete(ii);
                    }
                }
                carr.Compact();
            }
            Prune(controls);
            Prune(onCollideControls);
            Prune(onDestroyControls);
        }
        public void ClearControls() {
            controls.Empty();
            onCollideControls.Empty();
            onDestroyControls.Empty();
        }

        private readonly DMCompactingArray<BEHControl> controls = new(4);
        private readonly DMCompactingArray<BEHControl> onCollideControls = new(4);
        private readonly DMCompactingArray<BEHControl> onDestroyControls = new(4);

        public void IterateControls(BehaviorEntity beh) {
            for (int ii = 0; ii < controls.Count && !beh.dying; ++ii) {
                //Ignore controls that have been cancelled, as they may be invalid
                if (!controls[ii].cT.Cancelled)
                    controls[ii].action(beh, controls[ii].cT);
            }
        }
        public void IterateCollideControls(BehaviorEntity beh) {
            for (int ii = 0; ii < onCollideControls.Count; ++ii) {
                if (!onCollideControls[ii].cT.Cancelled)
                    onCollideControls[ii].action(beh, onCollideControls[ii].cT);
            }
        }
        
        public void IterateDestroyControls(BehaviorEntity beh) {
            for (int ii = 0; ii < onDestroyControls.Count; ++ii) {
                if (!onDestroyControls[ii].cT.Cancelled)
                    onDestroyControls[ii].action(beh, onDestroyControls[ii].cT);
            }
        }
    }
    
    private static readonly BEHStyleMetadata defaultMeta = new(null, null);
    
    /// <summary>
    /// Pool definitions for bullet styles that are active. Pools are deactivated on each scene and activated when used.
    /// </summary>
    private static readonly List<BEHStyleMetadata> activePools = new(16);

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
    private static readonly HashSet<string> ignoreCullStyles = new();

    //set by initialize > updatestyleinfo
    public BEHStyleMetadata myStyle { get; private set; } = defaultMeta;

    protected virtual void UpdateStyle(BEHStyleMetadata newStyle) {
        myStyle = newStyle;
        if (displayer != null) displayer.UpdateStyle(myStyle);
    }

    /// <summary>
    /// Repository for functions that can be applied to BehaviorEntities via the `beh-control` command,
    /// including complex bullets such as lasers and pathers.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Reflect]
    public static class BulletControls {
        /// <summary>
        /// Set the time of bullets.
        /// </summary>
        /// <param name="time">Time to set</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Time(float time, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) b.SetTime(time);
            }, BulletControl.P_MOVE_1);
        }
        /// <summary>
        /// Change the style of bullets.
        /// Note: should only be used between the same type, eg pather->pather, laser->laser. Otherwise, weird shit might happen.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Restyle(string target, Pred cond) {
            var style = GetPool(target);
            FrameAnimBullet.Recolor r = style.recolor?.GetOrLoadRecolor() ?? 
                                        throw new Exception($"Style {target} has no coloration");
            return new cBEHControl((b, cT) => {
                if (cond(b.rBPI)) {
                    ((ColorizableBullet)b).ColorizeOverwrite(r);
                    b.UpdateStyle(style);
                }
            }, BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Change the bullets into a softcull-type bullet rather than destroying them directly.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Softcull(string? target, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) {
                    if (target != null)
                        b.SpawnSimple(target);
                    b.InvokeCull();
                }
            }, BulletControl.P_CULL);
        }
        /// <summary>
        /// Run a spawn effect on objects.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl SpawnOn(string target, Pred cond) => new((b, cT) => {
            if (cond(b.rBPI)) b.SpawnSimple(target);
        }, BulletControl.P_RUN);
        
        /// <summary>
        /// Destroy bullets.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Cull(Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) b.InvokeCull();
            }, BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Destroy bullets or enemies, while spawning their death effects and running on-destroy controls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Poof(Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) b.Poof();
            }, BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Flip the X-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipX(Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) b.FlipVelX();
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the Y-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipY(Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) b.FlipVelY();
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipx>")]
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipXGT(BPY wall, Pred cond) {
            return new((b, cT) => {
                var bpi = b.rBPI;
                if (bpi.loc.x > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the left.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipx<")]
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipXLT(BPY wall, Pred cond) {
            return new((b, cT) => {
                var bpi = b.rBPI;
                if (bpi.loc.x < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(false, wall(bpi));
                    b.FlipVelX();
                }
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the top.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipy>")]
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipYGT(BPY wall, Pred cond) {
            return new((b, cT) => {
                var bpi = b.rBPI;
                if (bpi.loc.y > wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the bottom.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipy<")]
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl FlipYLT(BPY wall, Pred cond) {
            return new((b, cT) => {
                var bpi = b.rBPI;
                if (bpi.loc.y < wall(bpi) && cond(bpi)) {
                    b.rBPI.FlipSimple(true, wall(bpi));
                    b.FlipVelY();
                }
            }, BulletControl.P_MOVE_3);
        }
        
        /// <summary>
        /// Add to the x-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl DX(float by, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.x += by;
                }
            }, BulletControl.P_MOVE_2);
        }
        
        /// <summary>
        /// Add to the y-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl DY(float by, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) {
                    b.rBPI.loc.y += by;
                }
            }, BulletControl.P_MOVE_2);
        }
        /// <summary>
        /// Add to the time of objects.
        /// </summary>
        /// <param name="by">Delta time</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl DT(float by, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) {
                    b.SetTime(b.rBPI.t + by);
                }
            }, BulletControl.P_MOVE_1);
        }
        /// <summary>
        /// Create a sound effect.
        /// </summary>
        /// <param name="sfx">Sound effect</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl SFX(string sfx, Pred cond) {
            return new((b, cT) => {
                if (cond(b.rBPI)) ISFXService.SFXService.Request(sfx);
            }, BulletControl.P_RUN);
        }

        /// <summary>
        /// Freeze an object. It will still collide but it will not move.
        /// </summary>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Freeze(Pred cond) => new((b, cT) => {
            if (cond(b.rBPI)) b.nextUpdateAllowed = false;
        }, BulletControl.P_TIMECONTROL);

        [BDSL1Only]
        public static cBEHControl UpdateF((string target, ExBPY valuer)[] targets, Pred cond) {
            var ctargets = targets.Select(t => (t.target, valuer: Compilers.BPY(t.valuer))).ToArray();
            return new cBEHControl((b, cT) => {
                if (cond(b.rBPI)) {
                    var bpi = b.rBPI;
                    for (int ii = 0; ii < targets.Length; ++ii)
                        bpi.ctx.envFrame.Value<float>(ctargets[ii].target) = ctargets[ii].valuer(bpi);
                }
            }, BulletControl.P_SAVE);
        }

        [BDSL1Only]
        public static cBEHControl UpdateV2((string target, ExTP valuer)[] targets, Pred cond) {
            var ctargets = targets.Select(t => (t.target, valuer: Compilers.TP(t.valuer))).ToArray();
            return new cBEHControl((b, cT) => {
                if (cond(b.rBPI)) {
                    var bpi = b.rBPI;
                    for (int ii = 0; ii < targets.Length; ++ii) {
                        bpi.ctx.envFrame.Value<Vector2>(ctargets[ii].target) = ctargets[ii].valuer(bpi);
                    }
                }
            }, BulletControl.P_SAVE);
        }
        
        /// <summary>
        /// Run some code on bullets that pass the condition.
        /// </summary>
        [BDSL2Only] //NB: ErasedParametric cannot be reflected by BDSL1
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Exec(ErasedParametric code, Pred cond) {
            return new cBEHControl((b, cT) => {
                if (cond(b.rBPI))
                    code(b.bpi);
            }, BulletControl.P_SAVE);
        }

        /// <summary>
        /// Batch several commands together under one predicate.
        /// </summary>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl Batch(Pred cond, cBEHControl[] over) {
            var priority = over.Max(o => o.priority);
            var funcs = over.Select(o => o.action).ToArray();
            return new cBEHControl((b, cT) => {
                if (cond(b.rBPI)) {
                    for (int ii = 0; ii < over.Length; ++ii) 
                        funcs[ii](b, cT);
                }
            }, priority);
        }

        /// <summary>
        /// If the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl SM(Pred cond, StateMachine target) => new((b, cT) => {
            if (cond(b.rBPI)) {
                var exec = b.GetINode("f-pool-triggered", null);
                using var gcx = b.rBPI.ctx.RevertToGCX(target.Scope, exec);
                _ = exec.RunExternalSM(SMRunner.Cull(target, cT, gcx));
            }
        }, BulletControl.P_RUN);
        
        
        /// <summary>
        /// When the bullet collides, if the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl OnCollide(Pred cond, StateMachine target) => new((b, cT) => {
            if (cond(b.rBPI)) {
                var exec = b.GetINode("f-collide-triggered", null);
                using var gcx = b.rBPI.ctx.RevertToGCX(target.Scope, exec);
                _ = exec.RunExternalSM(SMRunner.Cull(target, cT, gcx));
            }
        }, BulletControl.P_ON_COLLIDE);
        
        /// <summary>
        /// When the entity is destroyed, if the condition is true, spawn an iNode at the position and run an SM on it.
        /// <br/>This only works on enemies and other destructible entities.
        /// </summary>
        [CreatesInternalScope(AutoVarMethod.None, true)]
        public static cBEHControl OnDestroy(Pred cond, StateMachine target) => new((b, cT) => {
            if (cond(b.rBPI)) {
                var exec = b.GetINode("f-destroy-triggered", null);
                using var gcx = b.rBPI.ctx.RevertToGCX(target.Scope, exec);
                _ = exec.RunExternalSM(SMRunner.Cull(target, cT, gcx));
            }
        }, BulletControl.P_ON_DESTROY);
    }
    
    public static void ControlBullets(GenCtx caller, GCXF<bool> persist, StyleSelector styles, cBEHControl control, ICancellee cT) {
        BEHControl pc = new BEHControl(caller, control, persist, cT);
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            GetPool(styles.Complex[ii]).AddBulletControlEOF(pc.Mirror());
        }
    }

    /// <summary>
    /// Repository for functions that can be applied to BehaviorEntities via the `behpool-control` command,
    /// including complex bullets such as lasers and pathers.
    /// These functions are applied to the metadata applied to each BehaviorEntity style,
    /// rather than the objects themselves.
    /// </summary>
    [Reflect]
    public static class PoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// </summary>
        /// <returns></returns>
        public static BehPF Reset() =>
            (pool, cT) => {
                GetPool(pool).ClearControls();
                return NullDisposable.Default;
            };

        /// <summary>
        /// Set whether or not a pool can cull bullets that are out of camera range.
        /// </summary>
        /// <param name="cullActive">True iff camera culling is allowed.</param>
        /// <returns></returns>
        public static BehPF AllowCull(bool cullActive) => 
            (pool, cT) => GetPool(pool).CameraCullable.AddConst(cullActive);

        /// <summary>
        /// Unconditionally softcull all bullets in a pool with an automatically-determined cull style.
        /// </summary>
        /// <param name="targetFormat">Base cull style, eg. cwheel</param>
        /// <returns></returns>
        public static BehPF SoftCullAll(string targetFormat) =>
            (pool, cT) => {
                GetPool(pool).AddBulletControlEOF(new BEHControl(GenCtx.Empty,
                    BulletControls.Softcull(
                        BulletManager.PortColorFormat(pool, new SoftcullProperties(targetFormat, null)),
                        _ => true), Consts.NOTPERSISTENT, cT));
                return NullDisposable.Default;
            };
    }
    
    public static IDisposable ControlPool(StyleSelector styles, BehPF control, ICancellee cT) {
        var tokens = new IDisposable[styles.Complex.Length];
        for (int ii = 0; ii < styles.Complex.Length; ++ii) {
            tokens[ii] = control(styles.Complex[ii], cT);
        }
        return new JointDisposable(null, tokens);
    }

    /// <summary>
    /// Instantaneously cull all complex NPC bullets on screen (including lasers),
    ///  using the definitions in props to determine the cull pool.
    /// <br/>If cullPools is provided, then only culls those pools.
    /// </summary>
    public static void Autocull(SoftcullProperties props, string[]? cullPools = null) {
        void CullPool(string? poolStr) {
            if (poolStr == null) return;
            if (!BulletManager.CheckComplexPool(poolStr, out var pool) || pool.IsPlayer) 
                return;
            if (!BulletManager.PortColorFormat(poolStr, props, out string? target)) 
                return;
            pool.AddBulletControlEOF(new BEHControl(GenCtx.Empty, 
                BulletControls.Softcull(target, _ => true), Consts.NOTPERSISTENT, null));
        }
        foreach (var pool in (cullPools ?? activePools.Select(x => x.style))) CullPool(pool);
    }

    public static void PrunePoolControls() {
        for (int ii = 0; ii < activePools.Count; ++ii) {
            activePools[ii].PruneControls();
        }
    }

    public static void ClearPoolControls() {
        for (int ii = 0; ii < activePools.Count; ++ii)
            activePools[ii].ClearControls();
    }
}
}