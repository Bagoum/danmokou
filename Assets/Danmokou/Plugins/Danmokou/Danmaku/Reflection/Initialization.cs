using System;
using System.Collections.Generic;
using System.Linq;
using Ex = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Text;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.Dialogue;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.SM;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Danmokou.Reflection {
public static partial class Reflector {
    /*private static readonly Assembly[] ReflectableAssemblies = {
        typeof(CoreAssemblyMarker).Assembly,
        typeof(DanmakuAssemblyMarker).Assembly
    };*/

    static Reflector() {
        Debug.Log($"Entered static: {Application.isPlaying}");
        if (!Application.isPlaying) return;
        foreach (var type in ReflectorUtils.ReflectableAssemblyTypes) {
            foreach (var ca in type.GetCustomAttributes()) {
                if (ca is ReflectAttribute ra) {
                    ReflectionData.RecordPublic(type, ra.returnType);
                    break;
                }
            }
        }

        InitializeEnumResolvers();
        AllowFuncification<TEx<float>>();
        AllowFuncification<TEx<bool>>(); //This will also allow stuff like (if + true false), which will error if you actually use it
        AllowFuncification<TEx<Vector2>>();
        AllowFuncification<TEx<Vector3>>();
        AllowFuncification<TEx<Vector4>>();
        AllowFuncification<TEx<V2RV2>>();

        void CreatePostAggregates(string method, string shortcut) {
            var mi = typeof(ExPostAggregators).GetMethod(method) ??
                     throw new Exception($"Couldn't find post-aggregator \"{method}\"");
            var attrs = Attribute.GetCustomAttributes(mi);
            var priority = 999;
            Type[]? types = null;
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
                //types = [ (float), (v2) ... ]
                foreach (var rt in types) {
                    CreateAggregateMethod(mi.MakeGenericMethod(rt));
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
    }

    private readonly struct PostAggregate {
        //public readonly Type sourceType;
        public readonly Type searchType;
        public readonly MethodInfo invoker;
        public object Invoke(object? a, object? b) => invoker.Invoke(null, new[] {a, b});
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

    private static void InitializeEnumResolvers() {
        void CEnum<E>((char first, E value)[] values) {
            Type e = typeof(E);
            SimpleFunctionResolver[e] = s => {
                char c = char.ToLower(s[0]);
                for (int ii = 0; ii < values.Length; ++ii) {
                    if (values[ii].first == c) return values[ii].value!;
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
                    if (s.StartsWith(values[ii].first)) return values[ii].value!;
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
            ("l1", TSMReflection.StandLocation.LEFT1),
            ("l2", TSMReflection.StandLocation.LEFT2),
            ("r1", TSMReflection.StandLocation.RIGHT1),
            ("r2", TSMReflection.StandLocation.RIGHT2),
            ("center", TSMReflection.StandLocation.CENTER)
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
        CEnum(new[] {
            ('t', Events.RuntimeEventType.Trigger),
            ('n', Events.RuntimeEventType.Normal),
            ('_', Events.RuntimeEventType.Normal)
        });
    }
}
}