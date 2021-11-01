using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Cancellation;
using Danmokou.Core;
using Danmokou.Danmaku.Options;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using JetBrains.Annotations;
using UnityEngine;
using static Danmokou.DMath.Functions.ExM;
using GCP = Danmokou.Danmaku.Options.GenCtxProperty;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.Reflection.Reflector;
using static Danmokou.Reflection.Compilers;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>>;

namespace Danmokou.Danmaku.Patterns {
/// <summary>
/// Functions that describe patterns performed instantaneously.
/// </summary>
[Reflect]
public static partial class SyncPatterns {
    public static SyncPattern Reexec(AsyncPattern ap) => sbh => 
        sbh.GCX.exec.RunRIEnumerator(ap(new AsyncHandoff(sbh)));

    /*
     * PASS-ALONG SYNCPATTERNS
     */

    /// <summary>
    /// Spawn the final bullet relative to the origin instead of the firing entity.
    /// If the firing entity is a rotated summon, rotational offset will still apply.
    /// </summary>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static SyncPattern World(SyncPattern sp) => _AsGSR(sp, GCP.Root(GCXF(Parametrics.Zero())));

    /// <summary>
    /// Spawn the final bullet relative to the origin instead of the firing entity.
    /// Also set the V2RV2 to zero and remove the effects of rotational offset.
    /// <br/>Use this command to setup empty-guided fires.
    /// </summary>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static SyncPattern Loc0(SyncPattern sp) => _AsGSR(sp, RootZero, GCP.Face(Facing.DEROT), Loc0Start);

    private static readonly GenCtxProperty RootZero = GCP.Root(GCXFRepo.V2Zero);
    private static readonly GenCtxProperty Loc0Start = GCP.Start(new GCRule[] {
        new GCRule<V2RV2>(ExType.RV2, "rv2", GCOperator.Assign, GCXFRepo.RV2Zero)
    });

    /// <summary>
    /// Spawn the final bullet relative to the origin instead of the firing entity.
    /// Also set the V2RV2 to zero and remove the effects of rotational offset.
    /// Also set the color of the fire.
    /// <br/>Use this command to setup empty-guided fires.
    /// </summary>
    /// <param name="color">Color of fire</param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static SyncPattern Loc0c(string color, SyncPattern sp) => _AsGSR(sp, RootZero, 
        GCP.Face(Facing.DEROT), GCP.Color(new[] { color }), Loc0Start);

    /// <summary>
    /// Add time to summoned bullets. They will simulate the missing time and start from the specified time.
    /// </summary>
    /// <param name="frames"></param>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static SyncPattern AddTime(GCXF<float> frames, SyncPattern sp) {
        return sbh => {
            using var sbh2 = new SyncHandoff(sbh.ch, sbh.timeOffset + frames(sbh.GCX) * ETime.FRAME_TIME);
            sp(sbh2);
        };
    }

    /// <summary>
    /// Equal to `gsr { start rv2.rx +=f rand from to } sp`
    /// </summary>
    public static SyncPattern RandomX(ExBPY from, ExBPY to, SyncPattern sp) => _AsGSR(sp,
        GenCtxProperty.Start(new GCRule[] {
            new GCRule<float>(ExType.Float, "rv2.rx", GCOperator.AddAssign,
                GCXF(x => ExM.Rand(from(x), to(x))))
        }));
    
    /// <summary>
    /// Equal to `gsr { start rv2.ry +=f rand from to } sp`
    /// </summary>
    public static SyncPattern RandomY(ExBPY from, ExBPY to, SyncPattern sp) => _AsGSR(sp,
        GenCtxProperty.Start(new GCRule[] {
            new GCRule<float>(ExType.Float, "rv2.ry", GCOperator.AddAssign,
                GCXF(x => ExM.Rand(from(x), to(x))))
        }));

    /// <summary>
    /// Play a sound effect and then run the child SyncPattern.
    /// </summary>
    /// <param name="style">Sound effect style</param>
    /// <param name="sp">Child SyncPattern to run unchanged</param>
    /// <returns></returns>
    public static SyncPattern PSSFX(string style, SyncPattern sp) => _AsGSR(sp, GCP.SFX(new[] { style}));

    /// <summary>
    /// Set the color of the fire.
    /// </summary>
    public static SyncPattern Color(string color, SyncPattern sp) => _AsGSR(sp, GCP.Color(new[] {color}));

    /// <summary>
    /// Set the color of the fire, merging wildcards in the reverse direction.
    /// </summary>
    public static SyncPattern ColorR(string color, SyncPattern sp) => _AsGSR(sp, GCP.ColorR(new[] {color}));


    /// <summary>
    /// Run the child SyncPattern twice, once without modification
    /// and once flipping the angle over the X-axis.
    /// </summary>
    /// <param name="sp">Child SyncPattern to repeat</param>
    /// <returns></returns>
    public static SyncPattern DoubleFlipY(SyncPattern sp) => _AsGSR(sp, GCP.Times(_ => 2),
        GCP.PostLoop(new GCRule[] {
            new GCRule<float>(ExType.Float, "rv2.a", GCOperator.Assign,
                GCXF(x => Mul(EN1, RV2A(Reference<V2RV2>("rv2")(x)))))
        }));
    /// <summary>
    /// Run the child SyncPattern twice, once without modification
    /// and once flipping the angle over the Y-axis.
    /// </summary>
    /// <param name="sp">Child SyncPattern to repeat</param>
    /// <returns></returns>
    public static SyncPattern DoubleFlipX(SyncPattern sp) => _AsGSR(sp, GCP.Times(_ => 2),
        GCP.PostLoop(new GCRule[] {
            new GCRule<float>(ExType.Float, "rv2.a", GCOperator.Assign,
                GCXF(x => Sub(ExC(180f), RV2A(Reference<V2RV2>("rv2")(x)))))
        }));
    /// <summary>
    /// Run the child SyncPattern twice, once without modification
    /// and once flipping the angle over the line Y=X.
    /// </summary>
    /// <param name="sp">Child SyncPattern to repeat</param>
    /// <returns></returns>
    public static SyncPattern DoubleFlipXY(SyncPattern sp) => _AsGSR(sp, GCP.Times(_ => 2),
        GCP.PostLoop(new GCRule[] {
            new GCRule<float>(ExType.Float, "rv2.a", GCOperator.Assign,
                GCXF(x => Sub(ExC(90f), RV2A(Reference<V2RV2>("rv2")(x)))))
        }));

    public static SyncPattern SetP(GCXF<float> p, SyncPattern sp) => _AsGSR(sp, GCP.SetP(p));

    #region TargetSync

    /// <summary>
    /// Add the angle from the executing BehaviorEntity to the target to the child SyncPattern.
    /// </summary>
    /// <param name="target">Target</param>
    /// <param name="syncPattern">Child SyncPattern to modify</param>
    /// <returns></returns>
    public static SyncPattern Target(GCXF<Vector2> target, SyncPattern[] syncPattern) => _AsGSR(syncPattern, GenCtxProperty.Target(RV2ControlMethod.ANG, target));


    /// <summary>
    /// Add the X-difference from the executing BehaviorEntity to the target to the child SyncPattern.
    /// </summary>
    /// <param name="target">Target</param>
    /// <param name="syncPattern">Child SyncPattern to modify</param>
    /// <returns></returns>
    public static SyncPattern TargetX(GCXF<Vector2> target, SyncPattern[] syncPattern)  => _AsGSR(syncPattern, GenCtxProperty.Target(RV2ControlMethod.RX, target));

    /// <summary>
    /// Add the Y-difference from the executing BehaviorEntity to the target to the child SyncPattern.
    /// </summary>
    /// <param name="target">Target</param>
    /// <param name="syncPattern">Child SyncPattern to modify</param>
    /// <returns></returns>
    public static SyncPattern TargetY(GCXF<Vector2> target, SyncPattern[] syncPattern)  => _AsGSR(syncPattern, GenCtxProperty.Target(RV2ControlMethod.RY, target));

    #endregion
    
    #region SimplifiedSP

    private static (string, ExTP)[] AutoSaveV2(string loc, string dir) => new[] {
        (loc, SBV2Repo.Loc()),
        (dir, SBV2Repo.Dir())
    };

    private static readonly (string, ExBPY)[] AutoSaveF = { };

    /// <summary>
    /// GuideEmpty with a random suffix and no saved floats.
    /// </summary>
    public static SyncPattern GuideEmpty2(ExBPY indexer, (string, ExTP)[] saveV2s, GCXU<VTP> emptyPath,
        SyncPattern[] guided) => GuideEmpty(null, indexer, saveV2s, AutoSaveF, emptyPath, guided);

    /// <summary>
    /// Set up an empty-guided fire.
    /// </summary>
    /// <param name="suffix">The suffix to use for underlying empty pool names. Do not overlap with any other guideempty functions. First character should be a period.</param>
    /// <param name="indexer">The indexing function applied to data hoisted on the empty bullet.</param>
    /// <param name="saveV2s">Vector2 values to save on the empty bullet.</param>
    /// <param name="saveFs">Float values to save on the empty bullet.</param>
    /// <param name="emptyPath">The movement path of the empty bullet.</param>
    /// <param name="guided">The child fires that follow the empty bullet. They have Loc0 applied to them.</param>
    /// <returns></returns>
    public static SyncPattern GuideEmpty(string? suffix, ExBPY indexer, (string, ExTP)[] saveV2s,
        (string, ExBPY)[] saveFs, GCXU<VTP> emptyPath, SyncPattern[] guided) =>
        _GuideEmpty(suffix, indexer, saveV2s, saveFs, emptyPath, guided, false);
    
    /// <summary>
    /// Set up an empty-guided fire for player bullets.
    /// </summary>
    public static SyncPattern PlayerGuideEmpty(string? suffix, ExBPY indexer, (string, ExTP)[] saveV2s,
        (string, ExBPY)[] saveFs, GCXU<VTP> emptyPath, SyncPattern[] guided) =>
        _GuideEmpty(suffix, indexer, saveV2s, saveFs, emptyPath, guided, true);
    
    private static SyncPattern _GuideEmpty(string? suffix, ExBPY indexer, (string, ExTP)[] saveV2s,
        (string, ExBPY)[] saveFs, GCXU<VTP> emptyPath, SyncPattern[] guided, bool isPlayer) {
        var emptySP = isPlayer ?
            AtomicPatterns.Simple(emptyPath, new SBOptions(new[] {SBOption.Player(0, 0, "null")})) :
            AtomicPatterns.S(emptyPath);
        if (string.IsNullOrEmpty(suffix) || suffix![0] != '.')
            suffix = $".{RNG.RandString(8)}";
        string estyle = $"{BulletManager.EMPTY}{suffix}";
        var controlsL = new List<BulletManager.cBulletControl>();
        if (saveV2s.Length > 0) {
            var data = new (ReflectEx.Hoist<Vector2> target, ExBPY indexer, ExTP valuer)[saveV2s.Length];
            for (int ii = 0; ii < saveV2s.Length; ++ii) {
                data[ii] = (new ReflectEx.Hoist<Vector2>(saveV2s[ii].Item1), indexer, saveV2s[ii].Item2);
            }
            controlsL.Add(new BulletManager.cBulletControl(
                BulletManager.SimpleBulletControls.SaveV2(data, _ => ExMPred.True())));
        }
        if (saveFs.Length > 0) {
            var data = new (ReflectEx.Hoist<float> target, ExBPY indexer, ExBPY valuer)[saveFs.Length];
            for (int ii = 0; ii < saveFs.Length; ++ii) {
                data[ii] = (new ReflectEx.Hoist<float>(saveFs[ii].Item1), indexer, saveFs[ii].Item2);
            }
            controlsL.Add(new BulletManager.cBulletControl(
                BulletManager.SimpleBulletControls.SaveF(data, _ => ExMPred.True())));
        }
        guided = guided.Select(Loc0).ToArray();
        return sbh => {
            var controls = new List<BulletManager.BulletControl>();
            for (int ii = 0; ii < controlsL.Count; ++ii)
                //See ParticleControl for explanation of why .Root is used here
                controls.Add(new BulletManager.BulletControl(controlsL[ii], BulletManager.Consts.PERSISTENT, sbh.ch.cT.Root));
            BulletManager.AssertControls(isPlayer ? BulletManager.GetOrMakePlayerCopy(estyle) : estyle, controls);
            using var emptySbh = sbh.Copy(estyle);
            emptySP(emptySbh);
            for (int ii = 0; ii < guided.Length; ++ii) {
                guided[ii](sbh);
            }
        };
    }
    
    #endregion
    private struct SPExecutionTracker {
        private LoopControl<SyncPattern> looper;
        //Dirty sync handoff information to pass to children.
        //It's less optimal performance-wise to copy SyncHandoff in GSR children
        // (especially since there's no benefit around asynchronicity),
        // so we modify sbh.
        private SyncHandoff sbh;
        public SPExecutionTracker(GenCtxProperties<SyncPattern> props, SyncHandoff sbh, out bool isClipped) {
            looper = new LoopControl<SyncPattern>(props, sbh.ch, out isClipped);
            this.sbh = sbh;
        }

        public bool Remains => looper.Remains;

        public bool PrepareIteration() => looper.PrepareIteration();

        public void DoIteration(SyncPattern[] target) {
            sbh.ch = looper.Handoff;
            if (looper.props.childSelect != null) {
                target[(int)looper.props.childSelect(looper.GCX) % target.Length](sbh);
            } else {
                for (int ii = 0; ii < target.Length; ++ii) {
                    target[ii](sbh);
                }
            }
        }
        public void FinishIteration() => looper.FinishIteration();
        public void AllDone(bool normalEnd) {
            looper.IAmDone(normalEnd);
            //Do not dispose SBH-- it will be disposed by the caller
        }
    }
    
    /// <summary>
    /// The generic S-level repeater function.
    /// Takes any number of functionality-modifying properties as an array.
    /// </summary>
    /// <param name="props">Array of properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GSR")]
    public static SyncPattern GSRepeat(GenCtxProperties<SyncPattern> props, SyncPattern[] target) {
        return sbh => {
            SPExecutionTracker exec = new SPExecutionTracker(props, sbh, out bool isClipped);
            if (isClipped) {
                exec.AllDone(false);
            } else {
                while (exec.Remains && exec.PrepareIteration()) {
                    exec.DoIteration(target);
                    exec.FinishIteration();
                }
                exec.AllDone(true);
            }
        };
    }

    /// <summary>
    /// Like GSRepeat, but has specific handling for the TIMES and rpp properties.
    /// <br/>Note that SyncPatterns are instantaneous and therefore the WAIT property is inapplicable.
    /// </summary>
    /// <param name="times">Number of invocations</param>
    /// <param name="rpp">Amount to increment rv2 between invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GSR2")]
    public static SyncPattern GSRepeat2(GCXF<float> times, GCXF<V2RV2> rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GSRepeat(new GenCtxProperties<SyncPattern>(props.Append(GenCtxProperty.Sync(times, rpp))), target);
    
    /// <summary>
    /// Like GSRepeat, but has specific handling for the TIMES and FRV2 properties.
    /// <br/>Note that SyncPatterns are instantaneous and therefore the WAIT property is inapplicable.
    /// </summary>
    /// <param name="times">Number of invocations</param>
    /// <param name="frv2">Local RV2 offset as a function of GCX state</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GSRf")]
    public static SyncPattern GSRepeatFRV2(GCXF<float> times, GCXF<V2RV2> frv2, GenCtxProperty[] props, SyncPattern[] target) =>
        GSRepeat(new GenCtxProperties<SyncPattern>(props.Append(GenCtxProperty.Times(times)).Append(GenCtxProperty.FRV2(frv2))), target);
    
    
    /// <summary>
    /// Like GSRepeat, but has specific handling for the TIMES property with CIRCLE.
    /// <br/>Note that SyncPatterns are instantaneous and therefore the WAIT property is inapplicable.
    /// </summary>
    /// <param name="times">Number of invocations</param>
    /// <param name="props">Other properties</param>
    /// <param name="target">Child SyncPatterns to run</param>
    /// <returns></returns>
    [Alias("GSR2c")]
    public static SyncPattern GSRepeat2c(GCXF<float> times, GenCtxProperty[] props, SyncPattern[] target) =>
        GSRepeat(new GenCtxProperties<SyncPattern>(props.Append(GenCtxProperty.TimesCircle(times))), target);
    
    /// <summary>
    /// Like GSRepeat, but has specific handling for the TIMES and rpp properties, where both are mutated by the difficulty.
    /// </summary>
    [Alias("GSR2dr")]
    public static SyncPattern GSRepeat2dr(ExBPY difficulty, ExBPY times, ExBPRV2 rpp, GenCtxProperty[] props, SyncPattern[] target) =>
        GSRepeat(new GenCtxProperties<SyncPattern>(props.Append(GenCtxProperty.SyncDR(difficulty, times, rpp))), target);

    private static SyncPattern _AsGSR(SyncPattern target, params GenCtxProperty[] props) =>
        _AsGSR(new[] {target}, props);
    private static SyncPattern _AsGSR(SyncPattern[] target, params GenCtxProperty[] props) =>
        GSRepeat(new GenCtxProperties<SyncPattern>(props), target);
}
}