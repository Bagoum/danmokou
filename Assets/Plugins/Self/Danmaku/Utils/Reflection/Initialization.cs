using System;
using System.Collections.Generic;
using System.Text;
using Core;
using Danmaku;
using DMath;
using SM;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExLVTP = System.Func<Danmaku.ITExVelocity, RTEx<float>, RTEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using ExGCXF = System.Func<DMath.TExGCX, TEx>;
using ExSBCF = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx>;
using ExSBPred = System.Func<Danmaku.TExSBC, TEx<int>, DMath.TExPI, TEx<bool>>;

public static partial class Reflector {

    static Reflector() {
        MathConfig = GenericReflectionConfig.ManyPublic(typeof(ExM), typeof (ExMLerps), typeof(ExMSamplers),
            typeof(ExMConditionals), typeof(ExMDifficulty), typeof(ExMConversions), typeof(ExMMod),
            typeof(ExMV2), typeof(ExMRV2), typeof(ExMV3), 
            typeof(ExMV4), typeof(ExMPred));
        foreach (var t in new[] { 
        #if NO_EXPR
            typeof(NoExprMath_1), typeof(NoExprMath_2), 
        #endif
            typeof(VTPRepo), typeof(SyncPatterns), typeof(AtomicPatterns),
            typeof(AsyncPatterns), typeof(Parametrics), typeof(Parametrics3), typeof(Parametrics4), typeof(MovementPatterns),
            typeof(BPYRepo), typeof(FXYRepo), typeof(ExtraMovementFuncs),
            typeof(PredicateLogic), typeof(BulletManager.SimpleBulletControls), typeof(CurvedTileRenderLaser.LaserControls),
            typeof(BehaviorEntity.BulletControls), typeof(BulletManager.SimplePoolControls), typeof(BehaviorEntity.PoolControls),
            typeof(CurvedTileRenderLaser.PoolControls), typeof(SBFRepo), typeof(SBV2Repo), 
            typeof(BPRV2Repo), typeof(GenCtxProperty), typeof(LaserOption), typeof(BehOption), typeof(SBOption), typeof(SBPredicates), typeof(Compilers),
            typeof(Synchronization), typeof(PhaseProperty), typeof(PatternProperty), typeof(Challenge)
        }) {
            ReflConfig.RecordPublic(t);
        }
        ReflConfig.RecordPublic<Events.EventDeclaration<Events.Event0>>(typeof(Events.Event0));
        ReflConfig.ShortcutAll("letdecl", ":::");
        ReflConfig.ShortcutAll("letv2s", "::v2");
        ReflConfig.ShortcutAll("divide", "/");
        ReflConfig.ShortcutAll("flipxgt", "flipx>");
        ReflConfig.ShortcutAll("flipxlt", "flipx<");
        ReflConfig.ShortcutAll("flipygt", "flipy>");
        ReflConfig.ShortcutAll("flipylt", "flipy<");
        InitializeEnumResolvers();
        AllowMath<TEx<float>, TEx<float>>();
        AllowMath<TExPI, TEx<float>>();
        AllowMath<TExPI, TEx<bool>>(); //This will allow stuff like (if + true false), which will erorr
        AllowMath<TExPI, TEx<Vector2>>();
        AllowMath<TExPI, TEx<Vector3>>();
        AllowMath<TExPI, TEx<Vector4>>();
        AllowMath<TExPI, TEx<V2RV2>>();
        AllowMath<RTExSB, TEx<float>>();
        AllowMath<RTExSB, TEx<bool>>();
        AllowMath<RTExSB, TEx<Vector2>>();
        AllowMath<RTExSB, TEx<Vector3>>();
        AllowMath<RTExSB, TEx<Vector4>>();
        AllowMath<RTExSB, TEx<V2RV2>>();
        foreach (var t in FallThroughOptions) {
            t.Value.Sort((x,y) => x.Item1.priority.CompareTo(y.Item1.priority));
        }
    }
    private static void InitializeEnumResolvers() {
        void CEnum<E>((char first, E value)[] values) {
            Type e = typeof(E);
            SimpleFunctionResolver[e] = s => {
                char c = char.ToLower(s[0]);
                for (int ii = 0; ii < values.Length; ++ii) {
                    if (values[ii].first == c) return values[ii].value;
                }
                StringBuilder sb = new StringBuilder();
                for (int ii = 0;;) {
                    sb.Append($"{values[ii].value} ({values[ii].first}");
                    if (++ii < values.Length) sb.Append("; ");
                    else break;
                }
                throw new NotImplementedException($"Enum {e.Name}.{s} does not exist. The valid values are:\n\t{sb}");
            };
        }
        void SEnum<E>((string first, E value)[] values) {
            Type e = typeof(E);
            SimpleFunctionResolver[e] = s => {
                for (int ii = 0; ii < values.Length; ++ii) {
                    if (s.StartsWith(values[ii].first)) return values[ii].value;
                }
                StringBuilder sb = new StringBuilder();
                for (int ii = 0;;) {
                    sb.Append($"{values[ii].value} ({values[ii].first})");
                    if (++ii < values.Length) sb.Append("; ");
                    else break;
                }
                throw new NotImplementedException($"Enum {e.Name}.{s} does not exist. The valid values are:\n\t{sb}");
            };
        }
        CEnum<LR>(new[] {
            ('l', LR.LEFT),
            ('r', LR.RIGHT)
        });
        CEnum<Enums.Parametrization>(new[] {
            ('t', Enums.Parametrization.THIS),
            ('d', Enums.Parametrization.DEFER),
            ('a', Enums.Parametrization.ADDITIVE),
            ('m', Enums.Parametrization.MOD),
            ('i', Enums.Parametrization.INVMOD),
        });
        CEnum<Enums.Blocking>(new[] {
            ('b', Enums.Blocking.BLOCKING),
            ('n', Enums.Blocking.NONBLOCKING)
        });
        CEnum<Enums.Facing>(new[] {
            ('o', Enums.Facing.ORIGINAL),
            ('d', Enums.Facing.DEROT),
            ('v', Enums.Facing.VELOCITY),
            ('r', Enums.Facing.ROTVELOCITY)
        });
        SEnum<Enums.PhaseType>(new[] {
            ("non", Enums.PhaseType.NONSPELL),
            ("spell", Enums.PhaseType.SPELL),
            ("final", Enums.PhaseType.FINAL),
            ("timeout", Enums.PhaseType.TIMEOUT),
            ("dialogue", Enums.PhaseType.DIALOGUE)
        });
        SEnum<ExType>(new[] {
            ("f", ExType.Float),
            ("v2", ExType.V2),
            ("v3", ExType.V3),
            ("rv", ExType.RV2),
        });
        SEnum<GCOperator>(new[] {
            ("=", GCOperator.Assign),
            ("+=", GCOperator.AddAssign),
            ("*=", GCOperator.MulAssign),
            ("-=", GCOperator.SubAssign),
            ("/=", GCOperator.DivAssign),
            ("//=", GCOperator.FDivAssign),
        });
        SEnum<Enums.SAAngle>(new[] {
            ("bo", Enums.SAAngle.ORIGINAL_BANK),
            ("br", Enums.SAAngle.REL_ORIGIN_BANK),
            ("bt", Enums.SAAngle.TANGENT_BANK),
            ("o", Enums.SAAngle.ORIGINAL)
        });
        SEnum<Enums.RV2ControlMethod>(new[] {
            ("nx", Enums.RV2ControlMethod.NX),
            ("ny", Enums.RV2ControlMethod.NY),
            ("rx", Enums.RV2ControlMethod.RX),
            ("ry", Enums.RV2ControlMethod.RY),
            ("a", Enums.RV2ControlMethod.ANG),
            ("ra", Enums.RV2ControlMethod.RANG)
        });
        SEnum<Emote>(new[] {
            ("_", Emote.NORMAL),
            ("norm", Emote.NORMAL),
            ("ang", Emote.ANGRY),
            ("hap", Emote.HAPPY),
            ("w", Emote.WORRY),
            ("c", Emote.CRY),
            ("su", Emote.SURPRISE),
            ("sp", Emote.SPECIAL)
        });
        SEnum<Dialoguer.StandLocation>(new[] {
            ("l1", Dialoguer.StandLocation.LEFT1),
            ("l2", Dialoguer.StandLocation.LEFT2),
            ("r1", Dialoguer.StandLocation.RIGHT1),
            ("r2", Dialoguer.StandLocation.RIGHT2),
            ("center", Dialoguer.StandLocation.CENTER)
        });
    }
}