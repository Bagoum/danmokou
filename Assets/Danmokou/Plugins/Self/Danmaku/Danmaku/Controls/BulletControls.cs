using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using DMK.Core;
using DMK.DataHoist;
using DMK.DMath;
using DMK.DMath.Functions;
using DMK.Expressions;
using DMK.Reflection;
using DMK.Services;
using JetBrains.Annotations;
using DMK.SM;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;
using ExSBF = System.Func<DMK.Expressions.RTExSB, DMK.Expressions.TEx<float>>;
using ExSBV2 = System.Func<DMK.Expressions.RTExSB, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using static DMK.Expressions.ExUtils;

namespace DMK.Danmaku {

public partial class BulletManager {
    public static class Consts {
        public static readonly Pred PERSISTENT = _ => true;
        public static readonly Pred NOTPERSISTENT = _ => false;
    }
    /// <summary>
    /// Simple bullet pool control descriptor.
    /// </summary>
    public readonly struct BulletControl {
        public readonly SBCF action;
        public readonly Pred persist;
        public readonly int priority;
        public readonly ICancellee cT;

        public BulletControl(SBCFc act, Pred persistent, [CanBeNull] ICancellee cT) {
            action = act.Func(this.cT = cT ?? Cancellable.Null);
            persist = persistent;
            this.priority = act.priority;
        }
        public BulletControl(SBCF act, Pred persistent, int priority, ICancellee cT) {
            action = act;
            persist = persistent;
            this.priority = priority;
            this.cT = cT;
        }

        public static bool operator ==(BulletControl b1, BulletControl b2) =>
            b1.action == b2.action && b1.persist == b2.persist && b1.priority == b2.priority;

        public static bool operator !=(BulletControl b1, BulletControl b2) => !(b1 == b2);

        public override int GetHashCode() => (action, persist, priority).GetHashCode();

        public override bool Equals(object o) => o is BulletControl bc && this == bc;

        //Pre-velocity
        public const int P_SETTINGS = -20;
        public const int P_TIMECONTROL = -10;
        public const int POST_VEL_PRIORITY = 0;
        public const int P_DEFAULT = 20;
        public const int P_MOVE_1 = 40;
        public const int P_MOVE_2 = 44;
        public const int P_MOVE_3 = 46;
        public const int POST_DIR_PRIORITY = 100;
        public const int P_SAVE = 110;
        public const int P_RUN = 130;
        //Cull is last in priority to reflect how it actually works. See the culling notes under Bullet Notes.md.
        public const int P_CULL = 140;
    }

    
    public class StyleSelector {
        private const char wildcard = '*';
        private readonly List<string[]> selections;
        private readonly List<string> enumerated;
        [CanBeNull] private string[] simple;
        [CanBeNull] private string[] complex;
        [CanBeNull] private string[] all;
        public string[] Simple => simple = simple ?? Styles(simpleBulletPools.Keys, "simple bullet").ToArray();
        public string[] Complex => complex = complex ?? Styles(behPools.Keys, "complex bullet").ToArray();
        public string[] All =>
            all = all ?? Styles(simpleBulletPools.Keys, "", false)
                .Concat(Styles(behPools.Keys, "", false)).ToArray();

        public StyleSelector(string[][] selections) {
            this.selections = new List<string[]>(selections);
            this.enumerated = Resolve(this.selections);
        }

        public StyleSelector(string one) : this(new[] {new[] {one}}) { }
        
        public static implicit operator StyleSelector(string s) => new StyleSelector(s);

        //each string[] is a list of `repeatcolorp`-type styles. 
        //we enumerate the entire selection by enumerating the cartesian product of selections,
        //then merging* the cartesian product, then enumerating from StylesFromKey if there are wildcards.
        //*Merging occurs by folding the cartesian product against the empty string with the following rules:
        // acc, x =>
        //     let ii = acc.indexOf('*')
        //     if (ii == -1) return x;
        //     return $"{acc.Substring(0, ii)}{x}{acc.Substring(ii+1)}";
        // ie. the first * is replaced with the next string.
        // This allows composing in any order. eg:
        // [ circle-*, ellipse-* ] [ */w, */b ] [ red, green ]
        private static string ComputeMerge(string acc, string newStyle) {
            for (int ii = 0; ii < acc.Length; ++ii) {
                if (acc[ii] == wildcard) {
                    //optimization for common edge cases
                    if (ii == 0) {
                        return acc.Length == 1 ? newStyle : $"{newStyle}{acc.Substring(1)}";
                    } else if (ii + 1 == acc.Length) {
                        return $"{acc.Substring(0, ii)}{newStyle}";
                    } else
                        return $"{acc.Substring(0, ii)}{newStyle}{acc.Substring(ii + 1)}";
                }
            }
            /*
            for (int ii = 0; ii < newStyle.Length; ++ii) {
                if (newStyle[ii] == wildcard) {
                    //optimization for common edge cases
                    if (ii == 0) return $"{acc}{newStyle.Substring(1)}";
                    if (ii + 1 == newStyle.Length) return $"{newStyle.Substring(0, ii)}{acc}";
                    return $"{newStyle.Substring(0, ii)}{acc}{newStyle.Substring(ii + 1)}";
                }
            }*/
            return newStyle;
        }
        public static string MergeStyles(string acc, string newStyle) {
            if (string.IsNullOrEmpty(acc) || acc == "_") return newStyle;
            if (string.IsNullOrEmpty(newStyle) || newStyle == "_") return acc;
            //This may look stupid, but merges in loops (eg. repeatcolorp) are extremely costly, and caching is way cheaper.
            if (!cachedMerges.TryGetValue(acc, out var againstDict)) {
                againstDict = cachedMerges[acc] = new Dictionary<string, string>();
            }
            if (!againstDict.TryGetValue(newStyle, out var merged)) {
                merged = againstDict[newStyle] = ComputeMerge(acc, newStyle);
            }
            return merged;
        }
        private static readonly Dictionary<string, Dictionary<string, string>> cachedMerges = new Dictionary<string, Dictionary<string, string>>();

        private static List<string> Resolve(List<string[]> selections) {
            Stack<int> indices = new Stack<int>();
            Stack<string> partials = new Stack<string>();
            List<string> done = new List<string>();
            partials.Push("");
            int iselection = 0;
            int ichoice = 0;
            while (true) {
                string merged = MergeStyles(partials.Peek(), selections[iselection][ichoice]);
                if (++iselection == selections.Count) {
                    done.Add(merged);
                    --iselection;
                } else {
                    partials.Push(merged);
                    indices.Push(ichoice);
                    ichoice = -1;
                }
                while (++ichoice == selections[iselection].Length) {
                    if (iselection == 0) goto Done;
                    --iselection;
                    partials.Pop();
                    ichoice = indices.Pop();
                }
            }
            Done:
            return done;
        }

        private IEnumerable<string> Styles(ICollection<string> styles, string errTyp, bool doErr = true) {
            for (int ii = 0; ii < enumerated.Count; ++ii) {
                var style = enumerated[ii];
                var pstyle = style;
                if (pstyle.IndexOf('.') > -1) pstyle = pstyle.Substring(0, pstyle.IndexOf('.'));
                if (styles.Contains(pstyle)) yield return style;
                else if (style.Length > 2 && style[0] == '*' && style[style.Length - 1] == '*') {
                    style = style.Substring(1, style.Length - 2);
                    foreach (var s in styles) {
                        if (s.Contains(style)) yield return s;
                    }
                } else if (style[0] == '*') {
                    style = style.Substring(1);
                    foreach (var s in styles) {
                        if (s.EndsWith(style)) yield return s;
                    }
                } else if (style[style.Length - 1] == '*') {
                    style = style.Substring(0, style.Length - 1);
                    foreach (var s in styles) {
                        if (s.StartsWith(style)) yield return s;
                    }
                } else if (style.IndexOf('*') > -1) {
                    var ic = style.IndexOf('*');
                    var s1 = style.Substring(0, ic);
                    var s2 = style.Substring(ic + 1);
                    foreach (var s in styles) {
                        if (s.StartsWith(s1) && s.EndsWith(s2)) yield return s;
                    }
                } else if (doErr) throw new InvalidDataException($"Not a valid {errTyp}: {style}");
            }
        }
    }

    /// <summary>
    /// DEPRECATED
    /// </summary>
    public static void ControlPoolSM(Pred persist, StyleSelector styles, StateMachine sm, ICancellee cT, Pred condFunc) {
        BulletControl pc = new BulletControl((sbc, ii, bpi) => {
            if (condFunc(bpi)) {
                var inode = sbc.GetINodeAt(ii, "pool-triggered", null, out uint sbid);
                using (var gcx = PrivateDataHoisting.GetGCX(sbid)) {
                    _ = inode.RunExternalSM(SMRunner.Cull(sm, cT, gcx));
                }
            }
        }, persist, BulletControl.P_RUN, cT);
        for (int ii = 0; ii < styles.Simple.Length; ++ii) {
            GetMaybeCopyPool(styles.Simple[ii]).AddPoolControl(pc);
        }
    }

    /// <summary>
    /// Convert a gradient-styled color into the equivalent gradient on another style.
    /// This works by looking for `-` as a color separator. It does not matter if the
    /// gradients/styles exist or not.
    /// If the source style is a copy-pool (eg. ellipse-green/w.2), the copy suffix is ignored.
    /// Returns true iff the style is gradient-styled; ie. has a '-' in it.
    /// </summary>
    /// <param name="fromStyle">Full name of a style, eg. circle-red/w</param>
    /// <param name="toStyleBase">Base name of target style, eg. cwheel</param>
    /// <param name="defaulter">Default color target if match is not found, eg. red/</param>
    /// <param name="target">New target style</param>
    /// <returns></returns>
    public static bool PortColorFormat(string fromStyle, string toStyleBase, string defaulter, out string target) {
        target = fromStyle;
        if (fromStyle.IndexOf('.') > -1) fromStyle = fromStyle.Substring(0, fromStyle.IndexOf('.'));
        for (int ii = fromStyle.Length - 1; ii >= 0; --ii) {
            if (fromStyle[ii] == '-') {
                var x = $"{toStyleBase}{fromStyle.Substring(ii)}";
                target = CheckOrCopyPool(x, out _) ? x : $"{toStyleBase}-{defaulter}";
                return true;
            }
        }
        return false;
    }
    public static string PortColorFormat(string fromStyle, string toStyleBase, string defaulter) => 
        PortColorFormat(fromStyle, toStyleBase, defaulter, out var target) ? target : $"{toStyleBase}-{defaulter}";

    /// <summary>
    /// Bullet controls for use with the `bullet-control` SM command. These deal with simple bullets.
    /// <br/>All controls have a `cond` argument, which is a filtering condition. The control only affects bullets for which the condition is satisfied.
    /// </summary>
    public static class SimpleBulletControls {
        /// <summary>
        /// Set the x-position of bullets.
        /// </summary>
        /// <param name="x">C value</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp X(ExSBF x, ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, sbc[ii].bpi.locx.Is(x(sbc[ii]))), BulletControl.P_MOVE_1);
        /// <summary>
        /// Set the y-position of bullets.
        /// </summary>
        /// <param name="y">Y value</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Y(ExSBF y, ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, sbc[ii].bpi.locy.Is(y(sbc[ii]))), BulletControl.P_MOVE_1);
        /// <summary>
        /// Set the time of bullets.
        /// </summary>
        /// <param name="time">Time to set</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Time(ExSBF time, ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, sbc[ii].bpi.t.Is(time(sbc[ii]))), BulletControl.P_MOVE_1);
        
        /// <summary>
        /// Change the style of bullets. Similar to copy, but the original is destroyed.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Restyle(string target, ExPred cond) {
            return new SBCFp((sbc, ii, bpi) => bpi.When(cond, Ex.Block(
                SimpleBulletCollection.addFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(target)), sbc, ii),
                sbc.Delete(ii)
            )), BulletControl.P_CULL);
        }

        /// <summary>
        /// Copy a bullet into another pool.
        /// </summary>
        /// <param name="style">Copied style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Copy(string style, ExPred cond) {
            return new SBCFp((sbc, ii, bpi) => bpi.When(cond,
                SimpleBulletCollection.copyFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(style)), sbc, ii)
            ), BulletControl.P_RUN);
        }
        /// <summary>
        /// Restyle a bullet and summon a softcull effect.
        /// </summary>
        /// <param name="copyStyle">Copied style</param>
        /// <param name="softcullStyle">Softcull style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp RestyleEffect(string copyStyle, string softcullStyle, ExPred cond) {
            return Batch(cond,
                new[] {Restyle(copyStyle, _ => ExMPred.True()), CopyNull(softcullStyle, _ => ExMPred.True())});
        }

        /// <summary>
        /// Copy (nondestructively) a bullet into another pool, with no movement.
        /// </summary>
        /// <param name="style">Copied style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp CopyNull(string style, ExPred cond) {
            return new SBCFp((sbc, ii, bpi) => bpi.When(cond, Ex.Block(
                SimpleBulletCollection.copyNullFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(style)), sbc, ii)
            )), BulletControl.P_RUN);
        }

        /// <summary>
        /// Change the bullets into a softcull-type bullet rather than destroying them directly.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Softcull(string target, ExPred cond) {
            SimpleBulletCollection toPool = GetMaybeCopyPool(target);
            if (toPool.MetaType != SimpleBulletCollection.CollectionType.Softcull) {
                throw new InvalidOperationException("Cannot softcull to a non-softcull pool: " + target);
            }
            return new SBCFp((sbc, ii, bpi) => bpi.When(cond, 
                //Note that we have to still use the getMaybeCopyPool since the pool may have been destroyed when the code is run
                SimpleBulletCollection.appendSoftcull.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(target)), sbc, ii)), BulletControl.P_CULL);
        }

        /// <summary>
        /// Softcull but without expressions. Used internally for runtime bullet controls.
        /// </summary>
        public static SBCFp Softcull_noexpr(string target, Pred cond) {
            SimpleBulletCollection toPool = GetMaybeCopyPool(target);
            if (toPool.MetaType != SimpleBulletCollection.CollectionType.Softcull) {
                throw new InvalidOperationException("Cannot softcull to a non-softcull pool: " + target);
            }
            return new SBCFp(ct => (sbc, ii, bpi) => {
                if (cond(bpi)) GetMaybeCopyPool(target).AppendSoftcull(sbc, ii);
            }, BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Destroy bullets.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Cull(ExPred cond) {
            return new SBCFp((sbc, ii, bpi) => bpi.When(cond, sbc.DeleteDestroy(ii)), BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Flip the X-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipX(ExPred cond) => new SBCFp((sbc, ii, bpi) => 
                bpi.When(cond, sbc[ii].velocity.FlipX()), BulletControl.P_MOVE_3);
        
        /// <summary>
        /// Flip the Y-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipY(ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, sbc[ii].velocity.FlipY()), BulletControl.P_MOVE_3);

        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipXGT(ExSBF wall, ExPred cond) => new SBCFp((sbc, ii, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(sbc[ii])),
                //This ordering is important: it allows using `flipx> xmax onlyonce _`
                Ex.IfThen(Ex.AndAlso(bpi.locx.GT(w), cond(bpi)), Ex.Block(
                    sbc[ii].bpi.FlipSimpleX(w),
                    sbc[ii].velocity.FlipX()
                ))
            );
        }, BulletControl.P_MOVE_3);

        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the left.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipXLT(ExSBF wall, ExPred cond) => new SBCFp((sbc, ii, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(sbc[ii])),
                Ex.IfThen(Ex.AndAlso(bpi.locx.LT(w), cond(bpi)), Ex.Block(
                    sbc[ii].bpi.FlipSimpleX(w),
                    sbc[ii].velocity.FlipX()
                ))
            );
        }, BulletControl.P_MOVE_3);

        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the top.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipYGT(ExSBF wall, ExPred cond) => new SBCFp((sbc, ii, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(sbc[ii])),
                Ex.IfThen(Ex.AndAlso(bpi.locy.GT(w), cond(bpi)), Ex.Block(
                    sbc[ii].bpi.FlipSimpleY(w),
                    sbc[ii].velocity.FlipY()
                ))
            );
        }, BulletControl.P_MOVE_3);

        /// <summary>
        /// Flip the y-velocity and y-position of bullets around a wall on the bottom.
        /// </summary>
        /// <param name="wall">Y-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp FlipYLT(ExSBF wall, ExPred cond) => new SBCFp((sbc, ii, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(sbc[ii])),
                Ex.IfThen(Ex.AndAlso(bpi.locy.LT(w), cond(bpi)), Ex.Block(
                    sbc[ii].bpi.FlipSimpleY(w),
                    sbc[ii].velocity.FlipY()
                ))
            );
        }, BulletControl.P_MOVE_3);

        /// <summary>
        /// Add to the x-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp DX(ExSBF by, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.locx, by(sbc[ii]))), BulletControl.P_MOVE_2);
        
        /// <summary>
        /// Add to the y-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp DY(ExSBF by, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.locy, by(sbc[ii]))), BulletControl.P_MOVE_2);
        
        /// <summary>
        /// Add to the time of bullets.
        /// </summary>
        /// <param name="by">Delta time</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp DT(ExSBF by, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.t, by(sbc[ii]))), BulletControl.P_MOVE_1);

        /// <summary>
        /// Change the throttling of bullets.
        /// </summary>
        /// <param name="by">Speedup ratio (1 = no effect, 2 = twice as fast, 0 = frozen, -1 = backwards)</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp Slowdown(ExSBF by, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, sbc.Speedup(by(sbc[ii]))), BulletControl.P_TIMECONTROL);

        /// <summary>
        /// Freeze an object. It will still collide but it will not move.
        /// <br/> Note: the semantics of this are slightly different from BehCF.Freeze.
        /// This function will run the update loop with a deltaTime of zero, so offset-based
        /// movement functions dependent on public hoisting may still cause movements.
        /// </summary>
        public static SBCFp Freeze(ExPred cond) => Slowdown(_ => 0f, cond);
        
        /// <summary>
        /// Create a sound effect.
        /// </summary>
        /// <param name="sfx">Sound effect</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static SBCFp SFX(string sfx, ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, SFXService.Request(Ex.Constant(sfx))), BulletControl.P_RUN);
       
        /// <summary>
        /// Add external velocity to bullets. Note that position-based movement (offset, polar)
        /// will probably not work properly with this.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <param name="path">External velocity</param>
        /// <returns></returns>
        public static SBCFp Force(ExPred cond, VTP path) {
            Movement vel = new Movement(path);
            return new SBCFp((sbc, ii, bpi) =>
                bpi.When(cond, vel.UpdateDeltaNoTime(sbc, ii)), BulletControl.P_MOVE_2);
        }

        /// <summary>
        /// Save vector2 values in public data hoisting.
        /// <br/>Note: This is automatically called by the GuideEmpty function.
        /// You should not need to use this yourself.
        /// </summary>
        /// <param name="targets">Several target, index, value tuples to save</param>
        /// <param name="cond">Filter condition</param>
        public static SBCFp SaveV2((ReflectEx.Hoist<Vector2> target, ExBPY indexer, ExSBV2 valuer)[] targets, ExPred cond) => new SBCFp((sbc, ii, bpi) => 
            bpi.When(cond, Ex.Block(targets.Select(t => t.target.Save(((Ex)t.indexer(bpi)).As<int>(), t.valuer(sbc[ii]))))), BulletControl.P_SAVE);
        
        public static SBCFp SaveV2_noexpr((ReflectEx.Hoist<Vector2> target, BPY indexer, SBV2 valuer)[] targets, Pred cond) {
            return new SBCFp(ct => (sbc, ii, bpi) => {
                if (cond(bpi)) {
                    foreach (var t in targets) {
                        t.target.Save((int) t.indexer(bpi), t.valuer(ref sbc[ii]));
                    }
                }
            }, BulletControl.P_SAVE);
        }
        
        /// <summary>
        /// Save float values in public data hoisting.
        /// <br/>Note: This is automatically called by the GuideEmpty function.
        /// You should not need to use this yourself.
        /// </summary>
        /// <param name="targets">Several target, index, value tuples to save</param>
        /// <param name="cond">Filter condition</param>
        public static SBCFp SaveF((ReflectEx.Hoist<float> target, ExBPY indexer, ExSBF valuer)[] targets, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, Ex.Block(targets.Select(t => t.target.Save(((Ex)t.indexer(bpi)).As<int>(), t.valuer(sbc[ii]))))), BulletControl.P_SAVE);
        
        public static SBCFp SaveF_noexpr((ReflectEx.Hoist<float> target, BPY indexer, SBF valuer)[] targets, Pred cond) {
            return new SBCFp(ct => (sbc, ii, bpi) => {
                if (cond(bpi)) {
                    foreach (var t in targets) {
                        t.target.Save((int) t.indexer(bpi), t.valuer(ref sbc[ii]));
                    }
                }
            }, BulletControl.P_SAVE);
        }

        /// <summary>
        /// Update existing V2 values in the private data hoisting for the bullet.
        /// </summary>
        public static SBCFp UpdateV2((string target, ExSBV2 valuer)[] targets, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, Ex.Block(targets.Select(t => PrivateDataHoisting.UpdateValue(bpi, Reflector.ExType.V2, t.target, t.valuer(sbc[ii]))))), BulletControl.P_SAVE);
        
        /// <summary>
        /// Update existing float values in the private data hoisting for the bullet.
        /// </summary>
        public static SBCFp UpdateF((string target, ExSBF valuer)[] targets, ExPred cond) => new SBCFp((sbc, ii, bpi) =>
            bpi.When(cond, Ex.Block(targets.Select(t => PrivateDataHoisting.UpdateValue(bpi, Reflector.ExType.Float, t.target, t.valuer(sbc[ii]))))), BulletControl.P_SAVE);

        /// <summary>
        /// Execute an event if the condition is satisfied.
        /// </summary>
        public static SBCFp Event(Events.Event0 ev, ExPred cond) => new SBCFp((sbc, ii, bpi) => bpi.When(cond, ev.exProc()), BulletControl.P_RUN);

        /// <summary>
        /// Batch several controls together under a single condition.
        /// <br/>This is useful primarily when `restyle` or `cull` is combined with other conditions.
        /// </summary>
        public static SBCFp Batch(ExPred cond, SBCFp[] over) {
            if (over.Any(o => o.func == null)) return BatchSM(Compilers.Pred(cond), over.Select(o => new SBCFc(o)).ToArray());
            var priority = over.Max(o => o.priority);
            return new SBCFp((sbc, ii, bpi) =>
                    bpi.When(cond, Ex.Block(over.Select(x => (Ex)x.func(sbc, ii, bpi)))), 
                priority);
        }
        /// <summary>
        /// Batch several controls together under a single condition.
        /// <br/>This is slightly slower but is compatible with the SM control.
        /// </summary>
        private static SBCFp BatchSM(Pred cond, SBCFc[] over) {
            var priority = over.Max(o => o.priority);
            return new SBCFp(ct => {
                var funcs = over.Select(o => o.Func(ct)).ToArray();
                return (sbc, ii, bpi) => {
                    if (cond(bpi)) {
                        for (int j = 0; j < funcs.Length; ++j) funcs[j](sbc, ii, bpi);
                    }
                };
            }, priority);
        }

        /// <summary>
        /// If the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        public static SBCFp SM(Pred cond, StateMachine target) => new SBCFp(cT => (sbc, ii, bpi) => {
            if (cond(bpi)) {
                var inode = sbc.GetINodeAt(ii, "pool-triggered", null, out uint sbid);
                //Note: this pattern is safe because GCX is copied immediately by SMRunner
                using (var gcx = PrivateDataHoisting.GetGCX(sbid)) {
                    gcx.fs["bulletTime"] = sbc[ii].bpi.t;
                    _ = inode.RunExternalSM(SMRunner.Cull(target, cT, gcx));
                }
            }
        }, BulletControl.P_RUN);

    }
    //Since sb controls are cleared immediately after velocity update,
    //it does not matter when in the frame they are added.
    public static void ControlPool(Pred persist, StyleSelector styles, SBCFc control, ICancellee cT) {
        BulletControl pc = new BulletControl(control, persist, cT);
        for (int ii = 0; ii < styles.Simple.Length; ++ii) {
            GetMaybeCopyPool(styles.Simple[ii]).AddPoolControl(pc);
        }
    }

    /// <summary>
    /// Pool controls for use with the `pool-control` SM command. These deal with simple bullets. As opposed to
    /// `bullet-control`, these commands affect the bullet pool instead of individual objects.
    /// </summary>
    public static class SimplePoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// </summary>
        /// <returns></returns>
        public static SPCF Reset() {
            return pool => GetMaybeCopyPool(pool).ClearControls();
        }
        /// <summary>
        /// Set the cull radius on a pool.
        /// This is reset automatically via clear phase.
        /// </summary>
        /// <returns></returns>
        public static SPCF CullRad(float r) {
            return pool => GetMaybeCopyPool(pool).SetCullRad(r);
        }

        /// <summary>
        /// Set whether or not a pool can cull bullets that are out of camera range.
        /// This is reset automatically via clear phase.
        /// </summary>
        /// <param name="cullActive">True iff camera culling is allowed.</param>
        /// <returns></returns>
        public static SPCF AllowCull(bool cullActive) {
            return pool => GetMaybeCopyPool(pool).allowCameraCull = cullActive;
        }
        /// <summary>
        /// Unconditionally softcull all bullets in a pool with an automatically-determined cull style.
        /// </summary>
        /// <param name="targetFormat">Base cull style, eg. cwheel</param>
        /// <returns></returns>
        public static SPCF SoftCullAll(string targetFormat) {
            return pool => GetMaybeCopyPool(pool).AddPoolControl(new BulletControl(new SBCFc(SimpleBulletControls.
                Softcull_noexpr(PortColorFormat(pool, targetFormat, "red/"), _ => true)), Consts.NOTPERSISTENT, null));
        }

        /// <summary>
        /// Tint the bullets in this pool. This is a multiplicative effect on the existing color.
        /// <br/> Note: This is a pool control, instead of a bullet option (as it is with lasers/pathers), to avoid bloating.
        /// <br/> Note: This can be used with all bullet styles, unlike Recolor.
        /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
        /// </summary>
        public static SPCF Tint(TP4 tint) => pool => 
            GetMaybeCopyPool(pool).SetTint(tint);
        
        /// <summary>
        /// Manually construct a two-color gradient for all bullets in this pool.
        /// <br/> Note: This is a pool control, instead of a bullet option (as it is with lasers/pathers), to avoid bloating.
        /// <br/> Note: This will error if you do not use it with the `recolor` palette.
        /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
        /// </summary>
        public static SPCF Recolor(TP4 black, TP4 white) => pool => 
            GetMaybeCopyPool(pool).SetRecolor(black, white);
    }
    
    public static void ControlPool(StyleSelector styles, SPCF control) {
        for (int ii = 0; ii < styles.Simple.Length; ++ii) {
            control(styles.Simple[ii]);
        }
    }

    /// <param name="targetFormat">Base cull style, eg. 'cwheel'</param>
    /// <param name="defaulter">Default color if no match is found, eg. 'red/'</param>
    public static void Autocull(string targetFormat, string defaulter) {
        void CullPool(SimpleBulletCollection pool) {
            if (pool.MetaType == SimpleBulletCollection.CollectionType.Softcull) return;
            if (pool.Count == 0) return;
            string targetPool = PortColorFormat(pool.Style, targetFormat, defaulter);
            pool.AddPoolControl(new BulletControl(new SBCFc(
                    SimpleBulletControls.Softcull_noexpr(targetPool, _ => true)), Consts.NOTPERSISTENT, null));
            //Log.Unity($"Autoculled {pool.style} to {targetPool}");
        }
        //CEmpty is destroyed via DestroyedCopyPools
        for (int ii = 0; ii < activeNpc.Count; ++ii) CullPool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) CullPool(activeCNpc[ii]);
    }

    /// <summary>
    /// For bombs/camera effects. Special bullets like EMPTY will not be deleted.
    /// </summary>
    public static void Autodelete(string targetFormat, string defaulter, Pred cond) {
        void DeletePool(SimpleBulletCollection pool) {
            if (!pool.Deletable || pool.Count == 0) return;
            string targetPool = PortColorFormat(pool.Style, targetFormat, defaulter);
            pool.AddPoolControl(new BulletControl(
                new SBCFc(SimpleBulletControls.Softcull_noexpr(targetPool, cond))
                , Consts.NOTPERSISTENT, null));
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) DeletePool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) DeletePool(activeCNpc[ii]);
    }
}
}