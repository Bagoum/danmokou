using System;
using System.Collections.Generic;
using System.Linq;
using Ex = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Text;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku;
using DMK.Danmaku.Options;
using DMK.Danmaku.Patterns;
using DMK.Dialogue;
using DMK.DMath;
using DMK.DMath.Functions;
using DMK.Expressions;
using DMK.GameInstance;
using DMK.Graphics;
using DMK.Scriptables;
using DMK.SM;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector3>>;
using ExFXY = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExBPRV2 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<DMK.DMath.V2RV2>>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;
using ExVTP = System.Func<DMK.Expressions.ITExVelocity, DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExLVTP = System.Func<DMK.Expressions.ITExVelocity, DMK.Expressions.RTEx<float>, DMK.Expressions.RTEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExGCXF = System.Func<DMK.Expressions.TExGCX, DMK.Expressions.TEx>;
using ExSBCF = System.Func<DMK.Expressions.TExSBC, DMK.Expressions.TEx<int>, DMK.Expressions.TExPI, DMK.Expressions.TEx>;
using ExSBPred = System.Func<DMK.Expressions.TExSBC, DMK.Expressions.TEx<int>, DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;

namespace DMK.Reflection {
public static partial class Reflector {

    static Reflector() {
        MathConfig = GenericReflectionConfig.ManyPublic(typeof(ExM), typeof(ExMLerps), typeof(ExMSamplers),
            typeof(ExMConditionals), typeof(ExMDifficulty), typeof(ExMConversions), typeof(ExMMod),
            typeof(ExMV2), typeof(ExMRV2), typeof(ExMV3),
            typeof(ExMV4), typeof(ExMPred));
        foreach (var t in new[] {
#if NO_EXPR
            typeof(NoExprMath_1), typeof(NoExprMath_2),
#endif
            typeof(VTPRepo), typeof(SyncPatterns), typeof(AtomicPatterns),
            typeof(AsyncPatterns), typeof(Parametrics), typeof(Parametrics3), typeof(Parametrics4),
            typeof(MovementPatterns),
            typeof(BPYRepo), typeof(FXYRepo), typeof(ExtraMovementFuncs),
            typeof(PredicateLogic), typeof(BulletManager.SimpleBulletControls),
            typeof(CurvedTileRenderLaser.LaserControls),
            typeof(BehaviorEntity.BulletControls), typeof(BulletManager.SimplePoolControls),
            typeof(BehaviorEntity.PoolControls),
            typeof(CurvedTileRenderLaser.PoolControls), typeof(SBFRepo), typeof(SBV2Repo),
            typeof(BPRV2Repo), typeof(GenCtxProperty), typeof(LaserOption), typeof(BehOption), typeof(SBOption),
            typeof(SBPredicates), typeof(Compilers),
            typeof(Synchronization), typeof(PhaseProperty), typeof(PatternProperty), typeof(ParsingProperty),
            typeof(Challenge)
        }) {
            ReflConfig.RecordPublic(t);
        }
        ReflConfig.RecordPublic<Events.EventDeclaration<Events.Event0>>(typeof(Events.Event0));
        ReflConfig.ShortcutAll("letdecl", ":::");
        ReflConfig.ShortcutAll("divide", "/");
        ReflConfig.ShortcutAll("flipxgt", "flipx>");
        ReflConfig.ShortcutAll("flipxlt", "flipx<");
        ReflConfig.ShortcutAll("flipygt", "flipy>");
        ReflConfig.ShortcutAll("flipylt", "flipy<");
        InitializeEnumResolvers();
        AllowMath<TEx<float>>();
        AllowMath<TEx<bool>>(); //This will allow stuff like (if + true false), which will erorr
        AllowMath<TEx<Vector2>>();
        AllowMath<TEx<Vector3>>();
        AllowMath<TEx<Vector4>>();
        AllowMath<TEx<V2RV2>>();
        AllowFuncMath<TEx<float>, TEx<float>>();
        AllowFuncMath<TExPI, TEx<float>>();
        AllowFuncMath<TExPI, TEx<bool>>(); //This will allow stuff like (if + true false), which will erorr
        AllowFuncMath<TExPI, TEx<Vector2>>();
        AllowFuncMath<TExPI, TEx<Vector3>>();
        AllowFuncMath<TExPI, TEx<Vector4>>();
        AllowFuncMath<TExPI, TEx<V2RV2>>();
        AllowFuncMath<RTExSB, TEx<float>>();
        AllowFuncMath<RTExSB, TEx<bool>>();
        AllowFuncMath<RTExSB, TEx<Vector2>>();
        AllowFuncMath<RTExSB, TEx<Vector3>>();
        AllowFuncMath<RTExSB, TEx<Vector4>>();
        AllowFuncMath<RTExSB, TEx<V2RV2>>();
        foreach (var t in FallThroughOptions) {
            t.Value.Sort((x, y) => x.Item1.priority.CompareTo(y.Item1.priority));
        }
        foreach (var t in UpwardsCastOptions) {
            t.Value.Sort((x, y) => x.Item1.priority.CompareTo(y.Item1.priority));
        }

        Type[] mathSourceTexTypes = {typeof(TEx<float>), typeof(TExPI), typeof(RTExSB)};
        void CreatePostAggregates(string method, string shortcut, Type[] texTypes = null) {
            texTypes = texTypes ?? mathSourceTexTypes;
            var mi = typeof(ExPostAggregators).GetMethod(method) ??
                     throw new Exception($"Couldn't find post-aggregator \"{method}\"");
            var attrs = Attribute.GetCustomAttributes(mi);
            var priority = 999;
            Type[] types = null;
            foreach (var attr in attrs) {
                if (attr is PAPriorityAttribute pp) priority = pp.priority;
                else if (attr is PASourceTypesAttribute ps) types = ps.types;
            }
            void CreateAggregateMethod(MethodInfo gmi) {
                var prms = gmi.GetParameters();
                if (prms.Length != 2) throw new Exception($"Post-aggregator \"{method}\" doesn't have 2 arguments");
                var sourceType = prms[0].ParameterType;
                var searchType = prms[1].ParameterType;
                if (gmi.ReturnType != sourceType)
                    throw new Exception($"Post-aggregator \"{method}\" has a different return and first argument type");
                postAggregators.SetDefaultSet(sourceType, shortcut,
                    new PostAggregate(priority, sourceType, searchType, gmi));
            }
            if (types == null) CreateAggregateMethod(mi);
            else {
                //teTypes = [ TExPI, RTexSB ... ]
                //types = [ (float), (v2) ... ]
                foreach (var st in texTypes) {
                    foreach (var rt in types) {
                        CreateAggregateMethod(mi.MakeGenericMethod(st, rt));
                    }
                }
            }
        }
        CreatePostAggregates("PA_Add", "+");
        CreatePostAggregates("PA_Mul", "*");
        CreatePostAggregates("PA_Sub", "-");
        CreatePostAggregates("PA_Div", "/");
        CreatePostAggregates("PA_FDiv", "//");
        CreatePostAggregates("PA_Pow", "^");
        CreatePostAggregates("PA_And", "&");
        CreatePostAggregates("PA_Or", "|");
#if NO_EXPR
        CreatePostAggregates("PA_Add_noexpr", "+");
        CreatePostAggregates("PA_Mul_noexpr", "*");
        CreatePostAggregates("PA_Sub_noexpr", "-");
        CreatePostAggregates("PA_Div_noexpr", "/");
#endif
        void CreatePreAggregates(string method, string shortcut, Type[] texTypes = null) {
            texTypes = texTypes ?? mathSourceTexTypes;
            var mi = typeof(ExPreAggregators).GetMethod(method) ??
                     throw new Exception($"Couldn't find post-aggregator \"{method}\"");
            var attrs = Attribute.GetCustomAttributes(mi);
            var priority = 999;
            var types = new Type[0];
            foreach (var attr in attrs) {
                if (attr is PAPriorityAttribute pp) priority = pp.priority;
                else if (attr is PASourceTypesAttribute ps) types = ps.types;
            }
            //teTypes = [ TExPI, RTexSB ... ]
            //types = [ (float), (v2) ... ]
            foreach (var st in texTypes) {
                foreach (var rt in types) {
                    var gmi = mi.MakeGenericMethod(st, rt);
                    var prms = gmi.GetParameters();
                    if (prms.Length != 2) throw new Exception($"Pre-aggregator \"{method}\" doesn't have 2 arguments");
                    var resultType = gmi.ReturnType;
                    var searchType1 = prms[0].ParameterType;
                    var searchType2 = prms[1].ParameterType;
                    if (preAggregators.TryGetValue(resultType, out var res)) {
                        if (res.firstType != searchType1)
                            throw new Exception(
                                $"Pre-aggregators currently support only one reducer type, " +
                                $"but return type {resultType.RName()} is associated with " +
                                $"{res.firstType.RName()} and {searchType1.RName()}.");
                    } else {
                        res = new PreAggregate(searchType1);
                    }
                    res.AddResolver(new PreAggregateResolver(shortcut, searchType2, gmi, priority));
                    preAggregators[resultType] = res;
                }
            }
        }

        CreatePreAggregates("PA_Mul", "*");
        CreatePreAggregates("PA_GT", ">");
        CreatePreAggregates("PA_LT", "<");
        CreatePreAggregates("PA_GEQ", ">=");
        CreatePreAggregates("PA_LEQ", "<=");
        CreatePreAggregates("PA_EQ", "=");
        CreatePreAggregates("PA_NEQ", "=/=");
        foreach (var key in preAggregators.Keys.ToArray()) {
            var res = preAggregators[key];
            res.SortResolvers();
            preAggregators[key] = res;
        }
    }

    private readonly struct PostAggregate {
        //public readonly Type sourceType;
        public readonly Type searchType;
        private readonly MethodInfo invoker;
        public object Invoke(object a, object b) => invoker.Invoke(null, new[] {a, b});
        public readonly int priority;

        public PostAggregate(int priority, Type source, Type search, MethodInfo mi) {
            this.priority = priority;
            //this.sourceType = source;
            this.searchType = search;
            this.invoker = mi;
        }
    }

    private static readonly Dictionary<Type, Dictionary<string, PostAggregate>> postAggregators =
        new Dictionary<Type, Dictionary<string, PostAggregate>>();

    private readonly struct PreAggregateResolver {
        public readonly string op;
        public readonly Type secondType;
        public readonly MethodInfo invoker;
        public object Invoke(object a, object b) => invoker.Invoke(null, new[] {a, b});
        public readonly int priority;

        public PreAggregateResolver(string op, Type type2, MethodInfo mi, int priority) {
            this.op = op;
            this.secondType = type2;
            this.invoker = mi;
            this.priority = priority;
        }
    }

    private readonly struct PreAggregate {
        //public readonly Type resultType;
        public readonly Type firstType;
        public readonly List<PreAggregateResolver> resolvers;
        public void AddResolver(PreAggregateResolver par) => resolvers.Add(par);
        public void SortResolvers() => resolvers.Sort((a, b) => a.priority.CompareTo(b.priority));

        public PreAggregate(Type type1) {
            firstType = type1;
            this.resolvers = new List<PreAggregateResolver>();
        }
    }

    private static readonly Dictionary<Type, PreAggregate> preAggregators = new Dictionary<Type, PreAggregate>();

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
        CEnum(new[] {
            ('l', LR.LEFT),
            ('r', LR.RIGHT)
        });
        CEnum(new[] {
            ('t', Parametrization.THIS),
            ('d', Parametrization.DEFER),
            ('a', Parametrization.ADDITIVE),
            ('m', Parametrization.MOD),
            ('i', Parametrization.INVMOD),
        });
        CEnum(new[] {
            ('b', Blocking.BLOCKING),
            ('n', Blocking.NONBLOCKING)
        });
        CEnum(new[] {
            ('o', Facing.ORIGINAL),
            ('d', Facing.DEROT),
            ('v', Facing.VELOCITY),
            ('r', Facing.ROTVELOCITY)
        });
        SEnum(new[] {
            ("non", PhaseType.NONSPELL),
            ("spell", PhaseType.SPELL),
            ("final", PhaseType.FINAL),
            ("timeout", PhaseType.TIMEOUT),
            ("survival", PhaseType.TIMEOUT),
            ("dialogue", PhaseType.DIALOGUE)
        });
        SEnum(new[] {
            ("f", ExType.Float),
            ("v2", ExType.V2),
            ("v3", ExType.V3),
            ("rv", ExType.RV2),
        });
        SEnum(new[] {
            ("=", GCOperator.Assign),
            ("+=", GCOperator.AddAssign),
            ("*=", GCOperator.MulAssign),
            ("-=", GCOperator.SubAssign),
            ("/=", GCOperator.DivAssign),
            ("//=", GCOperator.FDivAssign),
        });
        SEnum(new[] {
            ("bo", SAAngle.ORIGINAL_BANK),
            ("br", SAAngle.REL_ORIGIN_BANK),
            ("bt", SAAngle.TANGENT_BANK),
            ("o", SAAngle.ORIGINAL)
        });
        SEnum(new[] {
            ("nx", RV2ControlMethod.NX),
            ("ny", RV2ControlMethod.NY),
            ("rx", RV2ControlMethod.RX),
            ("ry", RV2ControlMethod.RY),
            ("a", RV2ControlMethod.ANG),
            ("ra", RV2ControlMethod.RANG)
        });
        SEnum(new[] {
            ("_", Emote.NORMAL),
            ("norm", Emote.NORMAL),
            ("ang", Emote.ANGRY),
            ("hap", Emote.HAPPY),
            ("w", Emote.WORRY),
            ("c", Emote.CRY),
            ("su", Emote.SURPRISE),
            ("sp", Emote.SPECIAL)
        });
        SEnum(new[] {
            ("l1", Dialoguer.StandLocation.LEFT1),
            ("l2", Dialoguer.StandLocation.LEFT2),
            ("r1", Dialoguer.StandLocation.RIGHT1),
            ("r2", Dialoguer.StandLocation.RIGHT2),
            ("center", Dialoguer.StandLocation.CENTER)
        });
        SEnum(new[] {
            ("none", ReflCtx.Strictness.NONE),
            ("comma", ReflCtx.Strictness.COMMAS)
        });
        CEnum(new[] {
            ('w', Palette.Shade.WHITE),
            ('h', Palette.Shade.HIGHLIGHT),
            ('l', Palette.Shade.LIGHT),
            ('p', Palette.Shade.PURE),
            ('d', Palette.Shade.DARK),
            ('o', Palette.Shade.OUTLINE),
            ('b', Palette.Shade.BLACK)
        });
    }
}
}