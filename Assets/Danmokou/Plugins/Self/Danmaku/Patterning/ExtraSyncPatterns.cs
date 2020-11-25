using System.Linq;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using static DMath.BPYRepo;
using GCP = Danmaku.GenCtxProperty;
using ExSBF = System.Func<Danmaku.RTExSB, TEx<float>>;
using ExSBV2 = System.Func<Danmaku.RTExSB, TEx<UnityEngine.Vector2>>;
using static Danmaku.Enums;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using SP = Danmaku.SyncPatterns;
using TPr = DMath.Parametrics;
using RV2r = DMath.BPRV2Repo;
using R = Reflector;
using static Reflector;
using static Compilers;
using static DMath.ExM;
using static DMath.ExMHelpers;
using static Danmaku.AtomicPatterns;
using static Danmaku.PatternUtils;
using static Danmaku.GenCtxProperty;
using static DMath.ExMMod;

namespace Danmaku {
public static class PatternUtils {
    public static ExBPRV2 BRV2 => ExM.Reference<V2RV2>("brv2");
    public static GCXF<V2RV2> RV2Zero => GCXFRepo.RV2Zero;

    public static ReflectEx.Hoist<Vector2> HV2(string key) => new ReflectEx.Hoist<Vector2>(key);
}

public static partial class AtomicPatterns {
    /// <summary>
    /// Empty-guided fire that points in the same direction as the empty bullet.
    /// <br/>Note: when following polar bullets, you should use this.
    /// </summary>
    public static SyncPattern DS(ReflectEx.Hoist<Vector2> hoistLoc, ReflectEx.Hoist<Vector2> hoistDir, ExBPY indexer,
        ExTP offset) =>
        Simple(GCXU(VTPRepo.DTPOffset(hoistLoc, hoistDir, indexer, offset)), new SBOptions(new[] {
            SBOption.Dir2(GCXU(x => RetrieveHoisted(hoistDir, indexer(x.bpi))))
        }));

    public static SyncPattern dPather() => Pather(0.5f, _ => 0.3f,
        "tprot px lerpt3 0 0.2 0.5 1 6 2 9".Into<GCXU<VTP>>(), new BehOptions());
}
public static partial class SyncPatterns {
    public static SyncPattern Aim1(ExBPY speed) => Target(GCXF(_ => LPlayer()), new[] {S(GCXU(VTPRepo.RVelocity(TPr.PX(speed))))});
    
    
    public static SyncPattern oArrowI(ExBPY times, ExBPY xstep, ExBPY ystep, GenCtxProperty[] props,
        SyncPattern[] inner) =>
        GSRepeat2(GCXF(times), RV2Zero, props.Prepend(
            PreLoop(new GCRule[] {
                new GCRule<float>(ExType.Float, "rv2.rx", GCOperator.Assign, GCXFf(x =>
                    RV2RX(BRV2(x)).Add(Neg(xstep(x).Mul(HMod(times(x), DecrementSubtract(times(x), x.t)))))
                )),
                new GCRule<float>(ExType.Float, "rv2.ry", GCOperator.Assign, GCXFf(x =>
                    RV2RY(BRV2(x)).Add(ystep(x).Mul(HNMod(times(x), DecrementSubtract(times(x), x.t))))
                )),
            })
        ).ToArray(), inner);

    private static string V2Key => PublicDataHoisting.GetRandomValidKey<Vector2>();

    private const string xi = "xi";
    private const string yi = "yi";
    private static readonly ExBPY rxi = Reference<float>(xi);
    private static readonly ExBPY ryi = Reference<float>(yi);
    private static SyncPattern _FArrow(ExBPY indexer, ExBPY n, ExBPY xstep, ExBPY ystep, GenCtxProperty[] props,
        GCXU<VTP> path, [CanBeNull] string poolSuffix, [CanBeNull] string locSave, [CanBeNull] string dirSave, params SyncPattern[] extraSp) {
        return GuideEmpty(poolSuffix, indexer, AutoSaveV2(locSave = locSave ?? V2Key, dirSave = dirSave ?? V2Key), 
            AutoSaveF, path, extraSp.Append(
            GSRepeat2(GCXFf(x => n(x).Mul(n(x).Add(E1)).Div(E2)), RV2Zero, new[] {
                PreLoop(new GCRule[] {
                    new GCRule<float>(ExType.Float, xi, GCOperator.Assign, GCXF(x => Floor(EN05.Add(
                            Sqrt(E025.Add(E2.Mul(x.t)))
                        )))), 
                    new GCRule<float>(ExType.Float, yi, GCOperator.Assign, GCXF(x => Sub(T()(x), E05.Mul(
                            Sqr(rxi(x)).Add(E2.Mul(rxi(x)))))
                        ))
                })
            }.Concat(props).ToArray(), new [] {
                DS(HV2(locSave), HV2(dirSave), P(), Parametrics.PXY(
                    x => EN1.Mul(xstep(x)).Mul(rxi(x)),
                    x => ystep(x).Mul(ryi(x))
                ))
            })
        ).ToArray());
    }

    public static SyncPattern FArrow(ExBPY indexer, ExBPY n, ExBPY xStep, ExBPY yStep, GenCtxProperty[] props,
        GCXU<VTP> path) => _FArrow(indexer, n, xStep, yStep, props, path, null, null, null);

    public static SyncPattern TreeArrow(ExBPY indexer, ExBPY n, ExBPY xStep, ExBPY yStep, GenCtxProperty[] props,
        GCXU<VTP> path, string treeColor, GCXF<float> treeXLen, ExBPY treeYLen, ExBPY treeXStep,
        ExBPY treeYStep) {
        var loc = V2Key;
        var dir = V2Key;
        return _FArrow(indexer, n, xStep, yStep, props, path, null, loc, dir, Color(treeColor, _AsGSR(
            _AsGSR(DS(HV2(loc), HV2(dir), indexer, Parametrics.PXY(rxi, ryi)),
                Times(treeXLen),
                PreLoop(new GCRule[] { 
                    new GCRule<float>(ExType.Float, xi, GCOperator.Assign, GCXF(
                            x => Mul(EN1, Add(Mul(xStep(x), Decrement(n(x))), Mul(treeXStep(x), T()(x))))
                        )) })
            ), 
            Times(GCXF(treeYLen)),
            PreLoop(new GCRule[] { 
                new GCRule<float>(ExType.Float, yi, GCOperator.Assign, GCXF(
                    x => Mul(treeYStep(x), HNMod(treeYLen(x), T()(x)))
                )) })
        )));
    }
}

public static partial class AsyncPatterns {
    public static AsyncPattern gEruption(GCXF<float> wait, GCXF<float> times, ExBPY angleOffset,
        ExBPY speed, ExBPY gravity) => Eruption(wait, times, angleOffset, speed, gravity, new GenCtxProperty[] { });
    public static AsyncPattern Eruption(GCXF<float> wait, GCXF<float> times, ExBPY angleOffset,
        ExBPY speed, ExBPY gravity, GenCtxProperty[] props) => _AsGCR(S(GCXU(VTPRepo.Velocity(
        TPr.PX(speed),
        TPr.PY(gravity)
    ))), props, GenCtxProperty.WT(wait, times), GenCtxProperty.PreLoop(new GCRule[] {
        new GCRule<float>(ExType.Float, "rv2.a", GCOperator.Assign,
            GCXF(x => Add(angleOffset(x), RV2A(BRV2(x)))))
    }));
}

}