using System;
using System.Collections.Generic;
using System.Linq;
using Ex = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Text;
using BagoumLib;
using BagoumLib.Reflection;
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
using DictExtensions = Danmokou.Core.DictExtensions;
using Vector3 = UnityEngine.Vector3;

namespace Danmokou.Reflection {
public static class RHelper {
    public static bool REFLECT_IN_EDITOR = false;
}
public static partial class Reflector {
    /*private static readonly Assembly[] ReflectableAssemblies = {
        typeof(CoreAssemblyMarker).Assembly,
        typeof(DanmakuAssemblyMarker).Assembly
    };*/

    static Reflector() {
#if UNITY_EDITOR
        if (!Application.isPlaying && !RHelper.REFLECT_IN_EDITOR) return;
#endif
        AllowFuncification<TEx<float>>();
        AllowFuncification<TEx<bool>>(); //This will also allow stuff like (if + true false), which will error if you actually use it
        AllowFuncification<TEx<Vector2>>();
        AllowFuncification<TEx<Vector3>>();
        AllowFuncification<TEx<Vector4>>();
        AllowFuncification<TEx<V2RV2>>();
        
        foreach (var type in ReflectorUtils.ReflectableAssemblyTypes) {
            if (type.GetCustomAttribute<ReflectAttribute>(false) is { } ra)
                ReflectionData.RecordPublic(type, ra.returnType);
        }

        foreach (var type in new[] {
                     typeof(Enumerable), typeof(ObservableExtensions),
                     typeof(BagoumLib.Extensions), typeof(ArrayExtensions), typeof(IEnumExtensions),
                     typeof(ListExtensions), typeof(DictExtensions), typeof(BagoumLib.DictExtensions),
                     typeof(NullableExtensions), typeof(EventExtensions)
                 }) {
            ReflectionData.RecordExtensionMethodsInClass(type);
        }

        InitializeEnumResolvers();

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
            void CreateAggregateMethod(MethodInfo gmi, Type? specialize = null) {
                var sig = MethodSignature.Get(gmi);
                if (specialize != null)
                    sig = (sig as GenericMethodSignature)!.Specialize(specialize);
                if (sig.Params.Length != 2) throw new StaticException($"Post-aggregator \"{method}\" doesn't have exactly 2 arguments");
                var sourceType = sig.Params[0].Type;
                var searchType = sig.Params[1].Type;
                if (sig.ReturnType != sourceType)
                    throw new StaticException($"Post-aggregator \"{method}\" has a different return and first argument type");
                postAggregators.SetDefaultSet(sourceType, shortcut,
                    new PostAggregate(priority, sourceType, searchType, sig));
            }
            if (types == null) 
                CreateAggregateMethod(mi);
            else {
                //types = [ (float), (v2) ... ]
                foreach (var rt in types) {
                    CreateAggregateMethod(mi, rt);
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
        
        WaitForPhaseSM = SMReflection.Wait(Synchronization.Time(_ => M.IntFloatMax));
    }

    private readonly struct PostAggregate {
        //public readonly Type sourceType;
        public readonly Type searchType;
        public readonly MethodSignature sig;
        public readonly int priority;

        public PostAggregate(int priority, Type source, Type search, MethodSignature mi) {
            this.priority = priority;
            //this.sourceType = source;
            this.searchType = search;
            this.sig = mi;
        }
    }

    private static readonly Dictionary<Type, Dictionary<string, PostAggregate>> postAggregators =
        new();

    public static readonly Dictionary<string, List<(Type, object)>> bdsl2EnumResolvers = new();

    private static void InitializeEnumResolvers() {
        void CEnum<E>((char first, E value)[] values) {
            Type e = typeof(E);
            SimpleFunctionResolver[e] = s => {
                char c = char.ToLower(s[0]);
                for (int ii = 0; ii < values.Length; ++ii) {
                    if (values[ii].first == c) return values[ii].value!;
                }
                StringBuilder sb = new();
                for (int ii = 0;;) {
                    sb.Append($"{values[ii].value} ({values[ii].first}");
                    if (++ii < values.Length) sb.Append("; ");
                    else break;
                }
                throw new NotImplementedException($"Enum {e.Name}.{s} does not exist. The valid values are:\n\t{sb}");
            };
        }
        void SEnum<E>(bool copyToBDSL2Enum, (string first, E value)[] values) {
            Type e = typeof(E);
            if (copyToBDSL2Enum)
                BDSL2Enum(values);
            SimpleFunctionResolver[e] = s => {
                for (int ii = 0; ii < values.Length; ++ii) {
                    if (s.StartsWith(values[ii].first)) return values[ii].value!;
                }
                StringBuilder sb = new();
                for (int ii = 0;;) {
                    sb.Append($"{values[ii].value} ({values[ii].first})");
                    if (++ii < values.Length) sb.Append("; ");
                    else break;
                }
                throw new NotImplementedException($"Enum {e.Name}.{s} does not exist. The valid values are:\n\t{sb}");
            };
        }
        void BDSL2Enum<E>((string first, E value)[] values) {
            Type e = typeof(E);
            foreach (var (first, value) in values) {
                bdsl2EnumResolvers.AddToList(first, (e, value!));
            }
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
        BDSL2Enum(new[] {
            ("this", Parametrization.THIS),
            ("defer", Parametrization.DEFER),
            ("add", Parametrization.ADDITIVE),
            ("mod", Parametrization.MOD),
            ("invmod", Parametrization.INVMOD),
        });
        CEnum(new[] {
            ('o', Facing.ORIGINAL),
            ('d', Facing.DEROT),
            ('v', Facing.VELOCITY),
            ('r', Facing.ROTATOR)
        });
        BDSL2Enum(new[] {
            ("original", Facing.ORIGINAL),
            ("derot", Facing.DEROT),
            ("velocity", Facing.VELOCITY),
            ("rotator", Facing.ROTATOR)
        });
        
        SEnum(true, new[] {
            ("non", PhaseType.Nonspell),
            ("spell", PhaseType.Spell),
            ("final", PhaseType.FinalSpell),
            ("timeout", PhaseType.Timeout),
            ("survival", PhaseType.Timeout),
            ("dialogue", PhaseType.Dialogue)
        });
        SEnum(false, new[] {
            ("f", ExType.Float),
            ("v2", ExType.V2),
            ("v3", ExType.V3),
            ("rv", ExType.RV2),
        });
        SEnum(false, new[] {
            ("=", GCOperator.Assign),
            ("+=", GCOperator.AddAssign),
            ("*=", GCOperator.MulAssign),
            ("-=", GCOperator.SubAssign),
            ("/=", GCOperator.DivAssign),
            ("//=", GCOperator.FDivAssign),
        });
        SEnum(false, new[] {
            ("bo", SAAngle.ORIGINAL_BANK),
            ("br", SAAngle.REL_ORIGIN_BANK),
            ("bt", SAAngle.TANGENT_BANK),
            ("o", SAAngle.ORIGINAL)
        });
        BDSL2Enum(new[] {
            ("bankoriginal", SAAngle.ORIGINAL_BANK),
            ("bankrelative", SAAngle.REL_ORIGIN_BANK),
            ("banktangent", SAAngle.TANGENT_BANK),
            ("original", SAAngle.ORIGINAL)
        });
        SEnum(false, new[] {
            ("nx", RV2ControlMethod.NX),
            ("ny", RV2ControlMethod.NY),
            ("rx", RV2ControlMethod.RX),
            ("ry", RV2ControlMethod.RY),
            ("a", RV2ControlMethod.ANG),
            ("ra", RV2ControlMethod.RANG)
        });
        BDSL2Enum(new[] {
            ("nx", RV2ControlMethod.NX),
            ("ny", RV2ControlMethod.NY),
            ("rx", RV2ControlMethod.RX),
            ("ry", RV2ControlMethod.RY),
            ("ang", RV2ControlMethod.ANG),
            ("rang", RV2ControlMethod.RANG),
        });
        SEnum(false, new[] {
            ("l1", TSMReflection.StandLocation.LEFT1),
            ("l2", TSMReflection.StandLocation.LEFT2),
            ("r1", TSMReflection.StandLocation.RIGHT1),
            ("r2", TSMReflection.StandLocation.RIGHT2),
            ("center", TSMReflection.StandLocation.CENTER)
        });
        SEnum(false, new[] {
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
        BDSL2Enum(new[] {
            ("trigger", Events.RuntimeEventType.Trigger),
            ("normal", Events.RuntimeEventType.Normal),
        });
    }
}
}