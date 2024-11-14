using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using BagoumLib;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Ex = System.Linq.Expressions.Expression;

// ReSharper disable HeuristicUnreachableCode
#pragma warning disable 162

namespace Danmokou.Expressions {
public static class BakeCodeGenerator {
    public class DMKObjectPrinter : CSharpObjectPrinter {
        
        public Func<object, string?>? ObjectLookup { get; set; }

        public override string Print(object? o) => FormattableString.Invariant(o switch {
            V2RV2 rv2 => 
                $"new V2RV2({rv2.nx}f, {rv2.ny}f, {rv2.rx}f, {rv2.ry}f, {rv2.angle}f)",
            Vector2 v2 =>
                $"new Vector2({v2.x}f, {v2.y}f)",
            Vector3 v3 =>
                $"new Vector3({v3.x}f, {v3.y}f, {v3.z}f)",
            Vector4 v4 =>
                $"new Vector4({v4.x}f, {v4.y}f, {v4.z}f, {v4.w}f)",
            CCircle c =>
                $"new CCircle({c.x}f, {c.y}f, {c.r}f)",
            CRect r =>
                $"new CRect({r.x}f, {r.y}f, {r.halfW}f, {r.halfH}f, {r.angle}f)",
            StyleSelector s =>
                $"new StyleSelector({base.Print(s.enumerated)}, {base.Print(s.exclude)})",
            LText lt => $"new LText({Print(lt.defaultValue)}, {Print(lt.variants)})",
            _ => $"{base.Print(o)}"
        });

        protected override FormattableString NoPrintMethodFallback(object obj) {
            if (ObjectLookup?.Invoke(obj) is {} s)
                return $"{s}";
            var typ = obj.GetType();
            if (obj is ReflectEx.IHoist h)
                return $"new ReflectEx.Hoist<{TypePrinter.Print(typ.GetGenericArguments()[0])}>({Print(h.Name)})";
            if (obj is IUncompiledCode uc)
                return $"new UncompiledCode<{TypePrinter.Print(typ.GetGenericArguments()[0])}>({Print(uc.Code)})";
            if (obj is BEHPointer behp)
                return $"BehaviorEntity.GetPointerForID(\"{behp.id}\")";
            if (obj is ETime.Timer { name: { } name })
                return $"ETime.Timer.GetTimer(\"{name}\")";
            if (obj is Delegate del && del.Method.IsStatic) {
                //Cases in-engine where we directly provide (Func<float,float>)Easers.EIOSine as an argument
                return $"(({TypePrinter.Print(typ)}){TypePrinter.Print(del.Method.DeclaringType!)}.{del.Method.Name})";
            }
            return base.NoPrintMethodFallback(obj);
        }
    }

    public static CookingContext Cook { get; } = new();


    public static IDisposable? OpenContext(CookingContext.KeyType type, string identifier) =>
#if EXBAKE_SAVE || EXBAKE_LOAD
        Cook.NewContext(type, identifier);
#else
        null;
#endif


    public static D BakeAndCompile<D>(this TEx ex, TExArgCtx tac, params ParameterExpression[] prms) {
#if EXBAKE_LOAD
        var loader = tac.Ctx.BakeTracker as ExBakeTracker.Load ??
                    throw new Exception("The expression baking tracker does not support saving!");
        return (Cook.CurrentServe ?? throw new Exception("Tried to load an expression with no active serve"))
            .Next<D>(loader.ProxyArguments.ToArray());
#endif
        var f = FlattenVisitor.Flatten(ex, true, true);
#if UNITY_EDITOR
        /*if (typeof(D) == typeof(Pred)) {
            Debug.Log($"Ex:{typeof(D).SimpRName()} " +
                      $"{new ExpressionPrinter { ObjectPrinter = new DMKObjectPrinter { FallbackToToString = true } }.LinearizePrint(ex)}");
        }*/
#endif
        var result = Ex.Lambda<D>(f, prms).Compile();
#if EXBAKE_SAVE
        var saver = tac.Ctx.BakeTracker as ExBakeTracker.Save ??
                    throw new Exception("The expression baking tracker does not support saving!");
        var sb = new StringBuilder();
        //TODO possibly remove replaceExVisitor and handle all constant stuff via the printer helper
        foreach (var (key, sfn) in saver.RecursiveFunctions)
            //give an explicit name to recursive functions so they can be referenced before they are compiled
            saver.HoistedReplacements[key] = Ex.Variable(sfn.FuncType!,
                CookingContext.FileContext.GeneratedFunc.ScriptFnReference(sfn));
        
        var constReplaced = new ReplaceExVisitor(saver.HoistedReplacements).Visit(ex);
        var flattened = FlattenVisitor.Flatten(constReplaced, true, false, obj => Cook.FindByValue(obj) is null);
        //handle the ExMHelpers.LookupTable references and others introduced by flattening
        var constReplaced2 = new ReplaceExVisitor(saver.HoistedReplacements).Visit(flattened);
        var linearized = new LinearizeVisitor().Visit(constReplaced2);
        //As the replaced EXs contain references to nonexistent variables, we don't want to actually compile it
        var rex = Ex.Lambda<D>(linearized, prms);
        foreach (var hoistVar in saver.HoistedVariables)
            sb.AppendLine(hoistVar);
        sb.Append("return ");
        var constToStr = new Dictionary<object, string>();
        var printer = new ExpressionPrinter() {ObjectPrinter = new DMKObjectPrinter() {ObjectLookup = obj => {
                if (Cook.FindByValue(obj) is { } fn)
                    return $"{fn.GetAsConstant()}";
                else if (constToStr.TryGetValue(obj, out var s))
                    return s;
                return null;
            }}};
        foreach (var (cobj, rep) in saver.HoistedConstants)
            constToStr[cobj] = printer.Print(rep);
        foreach (var (obj, repl) in saver.HoistedReplacements)
            if (obj is ConstantExpression { Value: not null } cex)
                constToStr[cex.Value] = printer.Print(repl);
        try {
            sb.Append(printer.Print(rex));
        } catch (Exception e) {
            Logs.LogException(e);
            sb.Append("default!");
        }
        sb.AppendLine(";");
        (Cook.CurrentBake ?? throw new Exception("An expression was compiled with no active bake"))
            .Add<D>(tac, sb.ToString(), saver.ProxyTypes.ToArray(), result);
#endif
        return result;
    }
    
#if UNITY_EDITOR
    public static void BakeExpressions(bool dryRun) {
        //These calls ensure that static reflections are correctly initialized
        _ = new Challenge.WithinC(0);
        _ = new Challenge.WithoutC(0);
        
        var typFieldsCache = new Dictionary<Type, List<(MemberInfo, ReflectIntoAttribute)>>();
        void LoadReflected(UnityEngine.Object go) {
            var typ = go.GetType();
            if (!typFieldsCache.TryGetValue(typ, out var members)) {
                members = typFieldsCache[typ] = new List<(MemberInfo, ReflectIntoAttribute)>();
                foreach (var m in typ.GetFields().Cast<MemberInfo>().Concat(typ.GetProperties())) {
                    foreach (var ra in m.GetCustomAttributes<ReflectIntoAttribute>()) {
                        members.Add((m, ra));
                    }
                }
            }
            foreach (var (m, ra) in members) {
                var val = (m is FieldInfo f) ? f.GetValue(go) : (m as PropertyInfo)!.GetValue(go);
                if (ra.resultType != null) {
                    if (val is string[] strs)
                        foreach (var s in strs)
                            s.IntoIfNotNull(ra.resultType);
                    else if (val is string str)
                        str.IntoIfNotNull(ra.resultType);
                    else if (val is RString rs)
                        rs.Get().IntoIfNotNull(ra.resultType);
                    else if (val is null) {
                        //generally caused by unfilled field, can be ignored.
                    } else
                        throw new Exception("ReflectInto has resultType set on an invalid property type: " +
                                            $"{typ.SimpRName()}.{m.Name}<{val.GetType().SimpRName()}/" +
                                            $"{ra.resultType.SimpRName()}>");
                }
            }
        }
        Logs.Log("Loading GameObject reflection properties...");
        foreach (var path in AssetDatabase.FindAssets("t:GameObject")
                     .Select(AssetDatabase.GUIDToAssetPath)) {
            try {
                foreach (var cmp in AssetDatabase.LoadAssetAtPath<GameObject>(path)
                             .GetComponentsInChildren(typeof(Component)))
                    LoadReflected(cmp);
            } catch (Exception e) {
                Logs.LogException(new Exception($"Failed to reflect GameObject at {path}", e));
            }
        }
        Logs.Log("Loading ScriptableObject reflection properties...");
        foreach (var path in AssetDatabase.FindAssets("t:ScriptableObject")
                     .Select(AssetDatabase.GUIDToAssetPath)) {
            try {
                LoadReflected(AssetDatabase.LoadAssetAtPath<ScriptableObject>(path));
            } catch (Exception e) {
                Logs.LogException(new Exception($"Failed to reflect GameObject at {path}", e));
            }
        }
        Logs.Log("Loading TextAssets for reflection...");
        var textAssets = AssetDatabase.FindAssets("t:TextAsset", 
            GameManagement.References.scriptFolders.Prepend("Assets/Danmokou/Patterns").ToArray())
            .Select(AssetDatabase.GUIDToAssetPath);
        foreach (var path in textAssets) {
            if (path.EndsWith(".txt") || path.EndsWith(".bdsl")) {
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (textAsset.text.StartsWith("//AOT:SKIP"))
                    continue;
                Logs.Log($"Loading script from file {path}");
                try {
                    StateMachine.CreateFromDump(textAsset.text, out _);
                } catch (Exception e1) {
                    try {
                        CompileHelpers.ParseAndCompileErased(textAsset.text);
                    } catch (Exception e2) {
                        Logs.UnityError($"Failed to parse {path}:\n" + Exceptions.PrintNestedException(
                            new AggregateException(e1, e2)));
                    }
                }
            }
        }
        Logs.Log("Invoking ReflWrap wrappers...");
        ReflWrap.InvokeAllWrappers();
        if (!dryRun) {
            ExportGeneratedCode();
        }
        Logs.Log("Expression baking complete.");
    }

    public static void ExportGeneratedCode() {
        Logs.Log("Exporting reflected code...");
        BakeCodeGenerator.Cook.Export();
    }
#endif
}
}