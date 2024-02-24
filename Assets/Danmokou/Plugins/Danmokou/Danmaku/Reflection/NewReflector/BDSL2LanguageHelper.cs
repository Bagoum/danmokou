using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Reflection;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Reflection;
using UnityEngine;

namespace Danmokou.Reflection2 {
public class BDSL2LanguageHelper : CoroutineRegularUpdater {
    public TextAsset? loadScript;
    public string targetType = "float";
    
    public override void FirstFrame() {
        ParseScript();
    }

    [ContextMenu("Parse script")]
    public void ParseScript() {
        if (loadScript != null) {
            var rtyp = Parser.TypeFromString(this.targetType);
            if (!rtyp.TryL(out var typ))
                Logs.Log($"`{targetType}` is not a valid type identifier.", level: LogLevel.ERROR);
            if ((typ ??= typeof(void)) == typeof(void))
                DebugErasedFromText(loadScript.text);
            else
                debugScript.Specialize(typ).Invoke(null, loadScript.text);
        }
    }

    public static void DebugFromText<T>(string text) {
        var (result, ef) = Helpers.ParseAndCompileValue<T>(text);
        var sb = new StringBuilder();
        sb.Append($"{nameof(BDSL2LanguageHelper)} parsed a script with a result of: {Print(result)} " +
                 $"({typeof(T).RName()}).\n");
        DebugRootEf(sb, ef);
        Logs.Log(sb.ToString());
    }
    
    public static void DebugErasedFromText(string text) {
        var ef = Helpers.ParseAndCompileErased(text);
        var sb = new StringBuilder();
        sb.Append($"{nameof(BDSL2LanguageHelper)} parsed a script with a void result.\n");
        DebugRootEf(sb, ef);
        Logs.Log(sb.ToString());
    }

    private static void DebugRootEf(StringBuilder sb, EnvFrame ef) {
        DebugEF(sb, ef, null);
        foreach (var (key, imp) in ef.Scope.ImportDecls) {
            sb.Append($"Import {imp.Name}:\n");
            DebugEF(sb, imp.Ef, key);
        }
    }

    private static void DebugEF(StringBuilder sb, EnvFrame ef, string? asImport) {
        void Header() {
            if (asImport != null) sb.Append("\t");
        }
        void Indent() => sb.Append(asImport != null ? "\t\t" : "\t");
        Header();
        sb.Append("Variables:\n");
        foreach (var vdecl in ef.Scope.AllVisibleVars.Where(v => !v.Name.StartsWith('$'))) {
            Indent();
            var vName = $"{vdecl.Name}<{vdecl.FinalizedType?.RName()}>";
            if (asImport != null)
                vName = $"{asImport}.{vName}";
            if (vdecl.ConstantValue.Try(out var v))
                sb.Append($"(const) {vName}: {Print(v.Value)}\n");
            else
                sb.Append($"{vName}: {Print(efVal.Specialize(vdecl.FinalizedType!).Invoke(null, ef, vdecl))}\n");
        }
        Header();
        sb.Append("Functions:\n");
        foreach (var fndecl in ef.Scope.AllVisibleScriptFns) {
            Indent();
            if (fndecl.IsConstant)
                sb.Append("(const) ");
            sb.Append(fndecl.AsSignature(asImport) + "\n");
        }
    }

    private static string Print(object? o) {
        if (o is null) return "null";
        if (o is IEnumerable ie) {
            var frags = from object? x in ie select Print(x);
            return $"{{ {string.Join(", ", frags)} }}";
        }
        return o.ToString();
    }

    private static readonly GenericMethodSignature debugScript =
        (GenericMethodSignature)MethodSignature.Get(typeof(BDSL2LanguageHelper).GetMethod(nameof(DebugFromText))!);
    private static readonly GenericMethodSignature efVal = (GenericMethodSignature)MethodSignature.Get(typeof(EnvFrame)
        .GetMethod(nameof(EnvFrame.NonRefValue), new[] { typeof(VarDecl) })!);

}
}