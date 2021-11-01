using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Services;
using JetBrains.Annotations;
using Danmokou.SM;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExSBCF = System.Func<Danmokou.Expressions.TExSBC, Danmokou.Expressions.TEx<int>, Danmokou.Expressions.TEx<BagoumLib.Cancellation.ICancellee>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx>;

namespace Danmokou.Danmaku {

public partial class BulletManager {
    public static class Consts {
        public static readonly Pred PERSISTENT = _ => true;
        public static readonly Pred NOTPERSISTENT = _ => false;
    }
    //Expression bullet control
    public readonly struct exBulletControl {
        public readonly ExSBCF func;
        public readonly int priority;
        public exBulletControl(ExSBCF f, int p) {
            func = f;
            priority = p;
        }
    }
    //Compiled bullet control
    public readonly struct cBulletControl {
        public readonly SBCF func;
        public readonly int priority;
        
        public cBulletControl(exBulletControl ex) : this(Compilers.SBCF(ex.func), ex.priority) { }
        
        //Don't expose this constructor. It's a problem if it gets picked up by reflection.
        private cBulletControl(SBCF func, int priority) {
            this.func = func;
            this.priority = priority;
        }

        public static cBulletControl NoExpr(SBCF func, int priority) => new cBulletControl(func, priority);

        //public static implicit operator cBulletControl(exBulletControl ex) => new cBulletControl(ex);
    }
    
    /// <summary>
    /// Simple bullet pool control descriptor.
    /// </summary>
    public readonly struct BulletControl {
        public readonly SBCF action;
        public readonly Pred persist;
        public readonly int priority;
        public readonly ICancellee cT;

        public BulletControl(cBulletControl act, Pred persistent, ICancellee? cT) {
            this.cT = cT ?? Cancellable.Null;
            action = act.func;
            persist = persistent;
            this.priority = act.priority;
        }

        public static bool operator ==(BulletControl b1, BulletControl b2) =>
            b1.action == b2.action && b1.persist == b2.persist && b1.priority == b2.priority && 
            ReferenceEquals(b1.cT, b2.cT);

        public static bool operator !=(BulletControl b1, BulletControl b2) => !(b1 == b2);

        public override int GetHashCode() => (action, persist, priority, cT).GetHashCode();

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

        public const int P_ON_COLLIDE = 300;
    }

    
    public class StyleSelector {
        private const char wildcard = '*';
        private readonly List<string> enumerated;
        private string[]? simple;
        private string[]? complex;
        private string[]? all;
        public string[] Simple => simple ??= Styles(simpleBulletPools.Keys, "simple bullet").ToArray();
        public string[] Complex => complex ??= Styles(behPools.Keys, "complex bullet").ToArray();
        public string[] All =>
            all ??= Styles(simpleBulletPools.Keys, "", false)
                .Concat(Styles(behPools.Keys, "", false)).ToArray();

        public StyleSelector(string[][] selections) {
            this.enumerated = Resolve(selections);
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

        private static List<string> Resolve(IReadOnlyList<string[]> selections) {
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
                if (styles.Contains(pstyle) || styles.Contains(style)) 
                    yield return style;
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
                } else if (CheckOrCopyPool(style, out _))
                    yield return style;
                else if (doErr) throw new InvalidDataException($"Not a valid {errTyp}: {style}");
            }
        }
    }


    /// <summary>
    /// Convert a gradient-styled color into the equivalent gradient on another style.
    /// This works by looking for `-` as a color separator. It does not matter if the
    /// gradients/styles exist or not.
    /// <br/>If the source style is a copy-pool (eg. ellipse-green/w.2), the copy suffix is ignored.
    /// Returns true iff the style is gradient-styled; ie. has a '-' in it.
    /// <br/>The out value will be written null if props.autocullTarget=null.
    /// </summary>
    /// <param name="fromStyle">Full name of a style, eg. circle-red/w</param>
    /// <param name="props">Container with information about color porting in autocullTarget.</param>
    /// <param name="target">New target style</param>
    /// <returns></returns>
    public static bool PortColorFormat(string fromStyle, in SoftcullProperties props, out string? target) {
        if (props.autocullTarget == null) {
            target = null;
            return true;
        }
        target = fromStyle;
        if (fromStyle.IndexOf('.') > -1) fromStyle = fromStyle.Substring(0, fromStyle.IndexOf('.'));
        for (int ii = fromStyle.Length - 1; ii >= 0; --ii) {
            if (fromStyle[ii] == '-') {
                var substrLen = props.sendToC ? (fromStyle.IndexOf('/') + 1 - ii) : (fromStyle.Length - ii);
                var x = $"{props.autocullTarget}{fromStyle.Substring(ii, substrLen)}";
                target = CheckOrCopyPool(x, out _) ? x : props.DefaultPool;
                return true;
            }
        }
        return false;
    }
    public static string? PortColorFormat(string fromStyle, in SoftcullProperties props) => 
        PortColorFormat(fromStyle, in props, out var target) ? target : props.DefaultPool;

    /// <summary>
    /// Bullet controls for use with the `bullet-control` SM command. These deal with simple bullets.
    /// <br/>All controls have a `cond` argument, which is a filtering condition. The control only affects bullets for which the condition is satisfied.
    /// </summary>
    [Reflect]
    public static class SimpleBulletControls {
        private const string sbName = "sbc_ele";
        /// <summary>
        /// Set the x-position of bullets.
        /// </summary>
        /// <param name="x">C value</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl X(ExBPY x, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
            bpi.When(cond, sbc[ii].bpi.locx.Is(x(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_1);
        /// <summary>
        /// Set the y-position of bullets.
        /// </summary>
        /// <param name="y">Y value</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Y(ExBPY y, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
            bpi.When(cond, sbc[ii].bpi.locy.Is(y(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_1);
        /// <summary>
        /// Set the time of bullets.
        /// </summary>
        /// <param name="time">Time to set</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Time(ExBPY time, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
            bpi.When(cond, sbc[ii].bpi.t.Is(time(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_1);
        
        /// <summary>
        /// Change the style of bullets, ie. transfer the bullet to another pool.
        /// </summary>
        /// <param name="target">New style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Restyle(string target, ExPred cond) {
            return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond, 
                TExSBC.transferFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(target)), sbc, ii)
            ), BulletControl.P_CULL);
        }

        /// <summary>
        /// Copy a bullet into another pool. A new ID will be given to the new bullet.
        /// </summary>
        /// <param name="style">Copied style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Copy(string style, ExPred cond) {
            return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond,
                TExSBC.copyFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(style)), sbc, ii)
            ), BulletControl.P_RUN);
        }
        /// <summary>
        /// Restyle a bullet and summon a softcull effect.
        /// <br/>If the softcull style is null or empty, then will instead spawn a cull bullet.
        /// </summary>
        /// <param name="copyStyle">Copied style</param>
        /// <param name="softcullStyle">Softcull style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl RestyleEffect(string copyStyle, string? softcullStyle, ExPred cond) {
            return Batch(cond,
                new[] {CopyNull(softcullStyle, _ => ExMPred.True()), Restyle(copyStyle, _ => ExMPred.True())});
        }

        /// <summary>
        /// Copy (nondestructively) a bullet into another pool, with no movement.
        /// <br/>If the style is null or empty, then will instead spawn a cull bullet.
        /// </summary>
        /// <param name="style">Copied style</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl CopyNull(string? style, ExPred cond) {
            if (string.IsNullOrWhiteSpace(style) || style == "_")
                return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond, sbc.MakeCulledCopy(ii)),
                    BulletControl.P_RUN);
            else
                return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond, Ex.Block(
                    TExSBC.copyNullFrom.InstanceOf(getMaybeCopyPool.Of(Ex.Constant(style)), sbc, ii, 
                        Ex.Constant(null, typeof(SoftcullProperties?)))
                )), BulletControl.P_RUN);
        }

        /// <summary>
        /// Change the bullets into a softcull-type bullet rather than destroying them directly.
        /// Also leaves behind an afterimage of the bullet as it gets deleted in a CulledPool.
        /// </summary>
        /// <param name="target">New style. Can be null or _ to skip the copying and only do the afterimage.</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Softcull(string? target, ExPred cond) {
            var toPool = NullableGetMaybeCopyPool(target);
            if (toPool != null && toPool.MetaType != AbsSimpleBulletCollection.CollectionType.Softcull) {
                throw new InvalidOperationException("Cannot softcull to a non-softcull pool: " + target);
            }
            return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond,
                //Note that we have to still use the getMaybeCopyPool since the pool may have been destroyed when the code is run
                //also it's cleaner for baking, even if it's technically no longer needed as of v8 :)
                sbc.Softcull(nullableGetMaybeCopyPool.Of(Ex.Constant(target, typeof(string))), ii)
                ), BulletControl.P_CULL);
        }

        /// <summary>
        /// Softcull but without expressions. Used internally for runtime bullet controls.
        /// </summary>
        [DontReflect]
        public static cBulletControl Softcull_noexpr(SoftcullProperties props, string? target, Pred cond) {
            var toPool = NullableGetMaybeCopyPool(target);
            if (toPool != null && toPool.MetaType != AbsSimpleBulletCollection.CollectionType.Softcull) {
                throw new InvalidOperationException("Cannot softcull to a non-softcull pool: " + target);
            }
            return cBulletControl.NoExpr((sbc, ii, bpi, ct) => {
                if (cond(bpi)) {
                    sbc.Softcull(NullableGetMaybeCopyPool(target), ii, props);
                }
            }, BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Destroy bullets.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Cull(ExPred cond) {
            return new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond, sbc.DeleteSB(ii)), BulletControl.P_CULL);
        }
        
        /// <summary>
        /// Flip the X-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl FlipX(ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
                bpi.When(cond, sbc[ii].velocity.FlipX()), BulletControl.P_MOVE_3);
        
        /// <summary>
        /// Flip the Y-velocity of bullets.
        /// Use <see cref="FlipXGT"/>, etc instead for flipping against walls.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl FlipY(ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
            bpi.When(cond, sbc[ii].velocity.FlipY()), BulletControl.P_MOVE_3);

        /// <summary>
        /// Flip the x-velocity and x-position of bullets around a wall on the right.
        /// </summary>
        /// <param name="wall">X-position of wall</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        [Alias("flipx>")]
        public static exBulletControl FlipXGT(ExBPY wall, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(bpi.AppendSB(sbName, sbc[ii]))),
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
        [Alias("flipx<")]
        public static exBulletControl FlipXLT(ExBPY wall, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(bpi.AppendSB(sbName, sbc[ii]))),
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
        [Alias("flipy>")]
        public static exBulletControl FlipYGT(ExBPY wall, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(bpi.AppendSB(sbName, sbc[ii]))),
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
        [Alias("flipy<")]
        public static exBulletControl FlipYLT(ExBPY wall, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var w = VFloat();
            return Ex.Block(new[] {w},
                w.Is(wall(bpi.AppendSB(sbName, sbc[ii]))),
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
        public static exBulletControl DX(ExBPY by, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.locx, by(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_2);
        
        /// <summary>
        /// Add to the y-position of bullets. Useful for teleporting around the sides.
        /// </summary>
        /// <param name="by">Delta position</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl DY(ExBPY by, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.locy, by(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_2);
        
        /// <summary>
        /// Add to the time of bullets.
        /// </summary>
        /// <param name="by">Delta time</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl DT(ExBPY by, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, ExUtils.AddAssign(sbc[ii].bpi.t, by(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_MOVE_1);

        /// <summary>
        /// Change the throttling of bullets.
        /// </summary>
        /// <param name="by">Speedup ratio (1 = no effect, 2 = twice as fast, 0 = frozen, -1 = backwards)</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl Slowdown(ExBPY by, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, sbc.Speedup(by(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_TIMECONTROL);

        /// <summary>
        /// Freeze an object. It will still collide but it will not move.
        /// <br/> Note: the semantics of this are slightly different from BehCF.Freeze.
        /// This function will run the update loop with a deltaTime of zero, so offset-based
        /// movement functions dependent on public hoisting may still cause movements.
        /// </summary>
        public static exBulletControl Freeze(ExPred cond) => Slowdown(_ => 0f, cond);
        
        /// <summary>
        /// Create a sound effect.
        /// </summary>
        /// <param name="sfx">Sound effect</param>
        /// <param name="cond">Filter condition</param>
        /// <returns></returns>
        public static exBulletControl SFX(string sfx, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => 
            bpi.When(cond, ServiceLocator.SFXRequest(Ex.Constant(sfx))), BulletControl.P_RUN);
       
        /// <summary>
        /// Add external velocity to bullets. Note that position-based movement (offset, polar)
        /// will probably not work properly with this.
        /// </summary>
        /// <param name="cond">Filter condition</param>
        /// <param name="path">External velocity</param>
        /// <returns></returns>
        public static exBulletControl Force(ExPred cond, VTP path) {
            Movement vel = new Movement(path);
            return new exBulletControl((sbc, ii, ct, bpi) => {
#if EXBAKE_SAVE
                var key_name = bpi.Ctx.NameWithSuffix("forceMov");
                bpi.Ctx.HoistedVariables.Add(FormattableString.Invariant(
                    $"var {key_name} = new Movement({BakeCodeGenerator.Cook.ObjectToFunctionHoister[path]});"));
                    bpi.Ctx.HoistedReplacements[Ex.Constant(vel)] = Ex.Variable(typeof(Movement), key_name);
#endif
                return bpi.When(cond, vel.UpdateDeltaNoTime(sbc, ii));
            }, BulletControl.P_MOVE_2);
        }

        /// <summary>
        /// Save vector2 values in public data hoisting.
        /// <br/>Note: This is automatically called by the GuideEmpty function.
        /// You should not need to use this yourself.
        /// </summary>
        /// <param name="targets">Several target, index, value tuples to save</param>
        /// <param name="cond">Filter condition</param>
        public static exBulletControl SaveV2((ReflectEx.Hoist<Vector2> target, ExBPY indexer, ExTP valuer)[] targets, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var extbpi = bpi.AppendSB(sbName, sbc[ii]);
            return bpi.When(cond,
                Ex.Block(targets.Select(t =>
                    t.target.Save(((Ex) t.indexer(extbpi)).As<int>(), t.valuer(extbpi), bpi))));
        }, BulletControl.P_SAVE);

        /// <summary>
        /// Save float values in public data hoisting.
        /// <br/>Note: This is automatically called by the GuideEmpty function.
        /// You should not need to use this yourself.
        /// </summary>
        /// <param name="targets">Several target, index, value tuples to save</param>
        /// <param name="cond">Filter condition</param>
        public static exBulletControl SaveF((ReflectEx.Hoist<float> target, ExBPY indexer, ExBPY valuer)[] targets, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) => {
            var extbpi = bpi.AppendSB(sbName, sbc[ii]);
            return bpi.When(cond,
                Ex.Block(targets.Select(t =>
                    t.target.Save(((Ex) t.indexer(extbpi)).As<int>(), t.valuer(extbpi), bpi))));
        }, BulletControl.P_SAVE);

        /// <summary>
        /// Update existing V2 values in the private data hoisting for the bullet.
        /// </summary>
        public static exBulletControl UpdateV2((string target, ExTP valuer)[] targets, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, Ex.Block(targets.Select(t => FiringCtx.SetValue(bpi, FiringCtx.DataType.V2, t.target, t.valuer(bpi.AppendSB(sbName, sbc[ii])))))), BulletControl.P_SAVE);
        
        /// <summary>
        /// Update existing float values in the private data hoisting for the bullet.
        /// </summary>
        public static exBulletControl UpdateF((string target, ExBPY valuer)[] targets, ExPred cond) => new exBulletControl((sbc, ii, ct, bpi) =>
            bpi.When(cond, Ex.Block(targets.Select(t => FiringCtx.SetValue(bpi, FiringCtx.DataType.Float, t.target, t.valuer(bpi.AppendSB(sbName, sbc[ii])))))), BulletControl.P_SAVE);

        /// <summary>
        /// Execute an event if the condition is satisfied.
        /// </summary>
        [GAlias(typeof(float), "eventf")]
        public static exBulletControl Event<T>(string ev, Func<TExArgCtx, TEx<T>> val, ExPred cond) => 
            new exBulletControl((sbc, ii, ct, bpi) => bpi.When(cond, 
                Events.exProcRuntimeEvent<T>().Of(Ex.Constant(ev), val(bpi.AppendSB(sbName, sbc[ii])))), BulletControl.P_RUN);

        /// <summary>
        /// Execute a unit event if the condition is satisfied.
        /// </summary>
        public static exBulletControl Event0<T>(string ev, ExPred cond) =>
            Event(ev, _ => new TEx<T>(Ex.Default(typeof(Unit))), cond);

        /// <summary>
        /// Batch several controls together under a single condition.
        /// <br/>This is useful primarily when `restyle` or `cull` is combined with other conditions.
        /// </summary>
        public static exBulletControl Batch(ExPred cond, exBulletControl[] over) =>
            BatchP(over.Max(o => o.priority), cond, over);
        
        /// <summary>
        /// Batchp several controls together under a single condition.
        /// <br/>Also, manually set the priority value. (Note that using BatchP instead of Batch can cause
        ///  severe bugs when cull-based commands are moved outside out of their standard processing block.)
        /// <br/>This is useful primarily when `restyle` or `cull` is combined with other conditions.
        /// </summary>
        public static exBulletControl BatchP(int priority, ExPred cond, exBulletControl[] over) {
            return new exBulletControl((sbc, ii, ct, bpi) => {
                    Ex NestRemaining(int index) {
                        if (index < over.Length) {
                            return Ex.Block(
                                over[index].func!(sbc, ii, ct, bpi),
                                Ex.IfThen(sbc.IsAlive(ii), NestRemaining(index + 1)));
                        } else
                            return Ex.Empty();
                    }
                    return bpi.When(cond, NestRemaining(0));
                }, 
                priority);
        }
        
        
        /// <summary>
        /// Batch several controls together under a single condition.
        /// <br/>This is slightly slower but is compatible with non-expression controls.
        /// </summary>
        private static cBulletControl Batch_noexpr(Pred cond, cBulletControl[] over) {
            Logs.Log("Batch_noexpr should not be used in most cases.", level: LogLevel.WARNING);
            var priority = over.Max(o => o.priority);
            return cBulletControl.NoExpr((sbc, ii, bpi, ct) => {
                if (cond(bpi)) {
                    for (int j = 0; j < over.Length; ++j) over[j].func(sbc, ii, bpi, ct);
                }
            }, priority);
        }

        /// <summary>
        /// If the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        public static exBulletControl SM(ExPred cond, StateMachine target) => new exBulletControl((sbc, ii, ct, bpi) => {
#if EXBAKE_SAVE
            bpi.Ctx.ProxyTypes.Add(typeof(StateMachine));
            bpi.Ctx.HoistedReplacements[Ex.Constant(target)] = Ex.Variable(typeof(StateMachine), bpi.Ctx.NextProxyArg());  
#elif EXBAKE_LOAD
            bpi.Ctx.ProxyArguments.Add(target);
#endif
            return bpi.When(cond, sbc.RunINodeAt(ii, Ex.Constant(target), ct));
        }, BulletControl.P_RUN);
        
        /// <summary>
        /// When a bullet collides, if the condition is true, spawn an iNode at the position and run an SM on it.
        /// </summary>
        public static exBulletControl OnCollide(ExPred cond, StateMachine target) => new exBulletControl((sbc, ii, ct, bpi) => {
#if EXBAKE_SAVE
            bpi.Ctx.ProxyTypes.Add(typeof(StateMachine));
            bpi.Ctx.HoistedReplacements[Ex.Constant(target)] = Ex.Variable(typeof(StateMachine), bpi.Ctx.NextProxyArg());  
#elif EXBAKE_LOAD
            bpi.Ctx.ProxyArguments.Add(target);
#endif
            return bpi.When(cond, sbc.RunINodeAt(ii, Ex.Constant(target), ct));
        }, BulletControl.P_ON_COLLIDE);

    }
    //Since sb controls are cleared immediately after velocity update,
    //it does not matter when in the frame they are added.
    public static void ControlPool(Pred persist, StyleSelector styles, cBulletControl control, ICancellee cT) {
        BulletControl pc = new BulletControl(control, persist, cT);
        for (int ii = 0; ii < styles.Simple.Length; ++ii) {
            GetMaybeCopyPool(styles.Simple[ii]).AddBulletControl(pc);
        }
    }

    /// <summary>
    /// Pool controls for use with the `pool-control` SM command. These deal with simple bullets. As opposed to
    /// `bullet-control`, these commands affect the bullet pool instead of individual objects.
    /// </summary>
    [Reflect]
    public static class SimplePoolControls {
        /// <summary>
        /// Clear the bullet controls on a pool.
        /// <br/>Note that this returns a null disposable.
        /// </summary>
        /// <returns></returns>
        public static SPCF Reset() {
            return (pool, cT) => {
                GetMaybeCopyPool(pool).ClearControls();
                return NullDisposable.Default;
            };
        }
        /// <summary>
        /// Set the cull radius on a pool.
        /// This is reset automatically via clear phase.
        /// </summary>
        /// <returns></returns>
        public static SPCF CullRad(float r) => 
            (pool, cT) => GetMaybeCopyPool(pool).BC.CULL_RAD.AddConst(r);

        /// <summary>
        /// Set whether or not a pool can cull bullets that are out of camera range.
        /// This is reset automatically via clear phase.
        /// </summary>
        /// <param name="cullActive">True iff camera culling is allowed.</param>
        /// <returns></returns>
        public static SPCF AllowCull(bool cullActive) => 
            (pool, cT) => GetMaybeCopyPool(pool).BC.AllowCameraCull.AddConst(cullActive);

        /// <summary>
        /// Set whether or not a pool's bullets can be deleted by effects
        ///  such as photos, bombs, and player damage clears.
        /// This is reset automatically via clear phase.
        /// </summary>
        public static SPCF AllowDelete(bool deleteActive) => 
            (pool, cT) => GetMaybeCopyPool(pool).BC.Deletable.AddConst(deleteActive);

        /// <summary>
        /// Unconditionally softcull all bullets in a pool with an automatically-determined cull style.
        /// <br/>Note that this returns a null disposable and is instead bounded by the cToken.
        /// </summary>
        /// <param name="targetFormat">Base cull style, eg. cwheel</param>
        /// <returns></returns>
        public static SPCF SoftCullAll(string targetFormat) =>
            (pool, cT) => {
                GetMaybeCopyPool(pool).AddBulletControl(new BulletControl(SimpleBulletControls.Softcull_noexpr(
                    new SoftcullProperties(null, null),
                    PortColorFormat(pool, new SoftcullProperties(targetFormat, null)),
                    _ => true), Consts.NOTPERSISTENT, cT));
                return NullDisposable.Default;
            };

        /// <summary>
        /// Tint the bullets in this pool. This is a multiplicative effect on the existing color.
        /// <br/> Note: This is a pool control, instead of a bullet option (as it is with lasers/pathers), to avoid bloating.
        /// <br/> Note: This can be used with all bullet styles, unlike Recolor.
        /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
        /// </summary>
        public static SPCF Tint(TP4 tint) => (pool, cT) => 
            GetMaybeCopyPool(pool).BC.Tint.AddConst(tint);
        
        /// <summary>
        /// Manually construct a two-color gradient for all bullets in this pool.
        /// <br/> Note: This is a pool control, instead of a bullet option (as it is with lasers/pathers), to avoid bloating.
        /// <br/> Note: This will error if you do not use it with the `recolor` palette.
        /// <br/> WARNING: This is a rendering function. Do not use `rand` (`brand` ok), or else replays will desync.
        /// </summary>
        public static SPCF Recolor(TP4 black, TP4 white) => (pool, cT) => 
            GetMaybeCopyPool(pool).SetRecolor(black, white);
    }
    
    public static IDisposable ControlPool(StyleSelector styles, SPCF control, ICancellee cT) {
        var tokens = new IDisposable[styles.Simple.Length];
        for (int ii = 0; ii < styles.Simple.Length; ++ii) {
            tokens[ii] = control(styles.Simple[ii], cT);
        }
        return new JointDisposable(null, tokens);
    }
    

    /// <summary>
    /// Instantaneously cull all NPC bullets on screen (not including empty bullets),
    ///  using the definitions in props to determine the cull pool.
    /// </summary>
    public static void Autocull(SoftcullProperties props) {
        void CullPool(SimpleBulletCollection pool) {
            if (!pool.SubjectToAutocull) return;
            var targetPool = PortColorFormat(pool.Style, props);
            pool.AddBulletControl(new BulletControl(
                    SimpleBulletControls.Softcull_noexpr(props, targetPool, _ => true), Consts.NOTPERSISTENT, null));
            Logs.Log($"Autoculled {pool.Style} to {targetPool ?? "None"}");
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) CullPool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) CullPool(activeCNpc[ii]);
    }

    /// <summary>
    /// For bombs/camera effects. Special bullets like EMPTY, as well as undeletable bullets like SUN
    /// and noncolliding bullets, will not be deleted.
    /// <br/>This will delete objects over time if the `advance` property on props is nonzero.
    /// </summary>
    public static void Autodelete(SoftcullProperties props, Pred cond) {
        void DeletePool(SimpleBulletCollection pool) {
            if (!pool.BC.Deletable.Value || !pool.SubjectToAutocull) return;
            var targetPool = PortColorFormat(pool.Style, props);
            pool.AddBulletControl(new BulletControl(SimpleBulletControls.Softcull_noexpr(props, targetPool, cond)
                , Consts.NOTPERSISTENT, null));
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) DeletePool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) DeletePool(activeCNpc[ii]);
    }
    
    public static void SoftScreenClear() => Autodelete(new SoftcullProperties(null, null), _ => true);
    
    /// <summary>
    /// For end-of-phase autoculling (from v8 onwards).
    /// <br/>This will operate over time using props.advance.
    /// <br/>Does not affect empty bullets, but does cull undeletable bullets (like sun).
    /// </summary>
    public static void AutocullCircleOverTime(SoftcullProperties props) {
        var timer = ETime.Timer.GetTimer(RNG.RandString());
        timer.Restart();
        //Don't have any mixups on the last frame...
        var effectiveAdvance = props.advance - 4 * ETime.FRAME_TIME;
        Pred survive = _ => timer.Seconds < effectiveAdvance;
        Pred deleteCond = bpi => {
            var dx = bpi.loc.x - props.center.x;
            var dy = bpi.loc.y - props.center.y;
            return Math.Min(1, (float)Math.Sqrt(dx * dx + dy * dy) / props.maxDist) <
                   //Avoid issues with the last few frames
                   (timer.Seconds + ETime.FRAME_TIME * 4) / effectiveAdvance;
        };
        var lprops = props.WithNoAdvance();
        void CullPool(SimpleBulletCollection pool) {
            if (!pool.SubjectToAutocull) return;
            var targetPool = PortColorFormat(pool.Style, lprops);
            pool.AddBulletControl(new BulletControl(
                SimpleBulletControls.Softcull_noexpr(lprops, targetPool, deleteCond), survive, null));
            Logs.Log($"DoT-Autoculling {pool.Style} to {targetPool}");
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) CullPool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) CullPool(activeCNpc[ii]);
    }
    
    /// <summary>
    /// For bombs/camera effects. Special bullets like EMPTY,
    ///  as well as undeletable bullets like SUN, will not be deleted.
    /// <br/>This will operate over time using props.advance.
    /// </summary>
    public static void AutodeleteCircleOverTime(SoftcullProperties props) {
        var timer = ETime.Timer.GetTimer(RNG.RandString());
        timer.Restart();
        Pred survive = _ => timer.Seconds < props.advance;
        Pred deleteCond = bpi => {
            var dx = bpi.loc.x - props.center.x;
            var dy = bpi.loc.y - props.center.y;
            return (float)Math.Sqrt(dx * dx + dy * dy) / props.maxDist <
                   (timer.Seconds + 2 * ETime.FRAME_TIME) / props.advance;
        };
        var lprops = props.WithNoAdvance();
        void DeletePool(SimpleBulletCollection pool) {
            if (!pool.BC.Deletable.Value || !pool.SubjectToAutocull) return;
            var targetPool = PortColorFormat(pool.Style, lprops);
            pool.AddBulletControl(new BulletControl(
                SimpleBulletControls.Softcull_noexpr(lprops, targetPool, deleteCond)
                , survive, null));
        }
        for (int ii = 0; ii < activeNpc.Count; ++ii) DeletePool(activeNpc[ii]);
        for (int ii = 0; ii < activeCNpc.Count; ++ii) DeletePool(activeCNpc[ii]);
    }
    
    
}
}