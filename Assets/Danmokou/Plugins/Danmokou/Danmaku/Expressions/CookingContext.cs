﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Expressions;
using BagoumLib.Reflection;
using Danmokou.Reflection;
using Scriptor.Analysis;
using Scriptor.Expressions;
using AST = Scriptor.Compile.AST;

namespace Danmokou.Expressions {
/// <summary>
/// A context responsible for either saving or loading all code generation in the game state.
/// </summary>
public class CookingContext {
    //private const string outputPath = "Assets/Danmokou/Plugins/Danmokou/Danmaku/Expressions/Generated/";
    public const string outputPath = "Assets/Danmokou/MiniProjects/Plugins/Danmokou/Generated/";
    private const string nmSpace = "Danmokou.Expressions";
    private const string clsName = "GeneratedExpressions_CG";
    private const string header = @"//----------------------
// <auto-generated>
//     Generated by Danmokou expression baking for use on AOT/IL2CPP platforms.
// </auto-generated>
//----------------------
#if EXBAKE_LOAD
using MiniProjects;
using MiniProjects.VN;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using UnityEngine;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Mathematics;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Expressions;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.DataHoist;
using Danmokou.Graphics;
using Danmokou.Graphics.Backgrounds;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.VN;
#pragma warning disable 162
#pragma warning disable 219";
    private const string footer = "#endif";
    // ReSharper disable once CollectionNeverQueried.Local
    private List<ExportedFile> GeneratedFiles { get; } = new();
    private HashSet<string> OpenedFileKeys { get; } = new();
    public Stack<FileContext> OpenContexts { get; } = new();
    public FileContext? CurrentFile => OpenContexts.TryPeek();
    public FileContext.Baker? CurrentBake => CurrentFile == null ? null :
        CurrentFile as FileContext.Baker ?? throw new Exception("Current context is not a bake");
    public FileContext.Server? CurrentServe => CurrentFile == null ? null :
        CurrentFile as FileContext.Server ?? throw new Exception("Current context is not a serve");

    public IDisposable NewContext(KeyType type, string key) {
    #if EXBAKE_SAVE
        var fileCtx = new FileContext.Baker(this, type, key);
        //It's still beneficial to open a duplicate context for the sake of record-keeping
        if (OpenedFileKeys.Contains(fileCtx.FileIdentifier))
            fileCtx.DoNotExport = true;
    #else
        var fileCtx = new FileContext.Server(this, type, key);
    #endif
        OpenedFileKeys.Add(fileCtx.FileIdentifier);
        OpenContexts.Push(fileCtx);
        return fileCtx;
    }
    
    public FileContext.GeneratedFunc? FindByValue(object? origValue) {
        if (origValue is null)
            return null;
        if (CurrentBake is {} b)
            for (int ii = 0; ii < b.GeneratedFunctions.Count; ++ii)
                if (b.GeneratedFunctions[ii].originalValue == origValue)
                    return b.GeneratedFunctions[ii];
        for (int ii = 0; ii < GeneratedFiles.Count; ++ii) {
            var sfns = GeneratedFiles[ii].ScriptFunctions;
            for (int jj = 0; jj < sfns.Count; ++jj)
                if (sfns[jj].originalValue == origValue)
                    return sfns[jj];
        }
        return null;
    }

    private void DisposeBake(FileContext.Baker fbc) {
        if (fbc != CurrentFile) throw new Exception("Tried to dispose the wrong FileBakeContext");
        if (fbc == null) throw new Exception("Dispose FileBakeContext should not be null");
        if (fbc.Export().Try(out var gf))
            GeneratedFiles.Add(gf);
        OpenContexts.Pop();
    }
    private void DisposeServe(FileContext.Server fbc) {
        if (fbc != CurrentFile) throw new Exception("Tried to dispose the wrong FileServeContext");
        if (fbc == null) throw new Exception("Dispose FileServeContext should not be null");
        //No extra action needs to be taken
        OpenContexts.Pop();
    }

    private const int FUNCS_PER_FILE = 300;

    public void Export() {
#if EXBAKE_SAVE
        var generatedCount = 0;
        var currentFuncs = new List<string>();
        void ExportFuncs() {
            FileUtils.WriteString(Path.Combine(outputPath, $"Generated{generatedCount++}.cs"), 
                WrapInClass(string.Join("\n", currentFuncs)));
            currentFuncs.Clear();
        }
        void AddFuncs(IEnumerable<string> funcs) {
            foreach (var f in funcs) {
                currentFuncs.Add(f);
                if (currentFuncs.Count >= FUNCS_PER_FILE) {
                    ExportFuncs();
                }
            }
        }
        var dictSB = new StringBuilder();
        dictSB.AppendLine($"\tpublic static readonly Dictionary<string, List<object>> _allDataMap = new() {{");
        
        foreach (var gf in GeneratedFiles) {
            AddFuncs(gf.funcDefs);
            var funcs = $"new List<object>() {{\n\t{string.Join(",\n\t", gf.funcsAsObjects)}\n}}";
            dictSB.AppendLine($"\t{{ \"{gf.filename}\", {funcs.Replace("\n", "\n\t")} }},");
        }
        ExportFuncs();
        dictSB.AppendLine("};");
        FileUtils.WriteString(Path.Combine(outputPath, "Top.cs"), WrapInClass(dictSB.ToString(), true));
        GeneratedFiles.Clear();
#endif
    }

    private static string WrapInClass(string inner, bool addAttr = false) {
        var attr = addAttr ? $"\n[GeneratedExpressions]" : "";
        return $@"{header}

namespace {nmSpace} {{{attr}
internal static partial class {clsName} {{
{inner.Replace("\n", "\n\t")}
}}
}}
{footer}
";
    }

    public readonly struct ExportedFile {
        public readonly KeyType type;
        public readonly string filename;
        public readonly List<FileContext.GeneratedFunc> ScriptFunctions;
        public readonly IEnumerable<string> funcDefs;
        public string FuncText => string.Join("\n", funcDefs);
        public readonly IEnumerable<string> funcsAsObjects; 
        
        public ExportedFile(KeyType type, string filename, IEnumerable<FileContext.GeneratedFunc> scriptFns, IEnumerable<string> funcDefs, IEnumerable<string> funcsAsObjects) {
            this.type = type;
            this.filename = filename;
            this.ScriptFunctions = scriptFns.ToList();
            this.funcDefs = funcDefs;
            this.funcsAsObjects = funcsAsObjects;
        }
    }

    public enum KeyType {
        /// <summary>
        /// SM.CreateFromDump
        /// </summary>
        SM,
        /// <summary>
        /// SMManager.LoadImport
        /// </summary>
        SM_IMPORT,
        /// <summary>
        /// string.Into
        /// </summary>
        INTO,
        /// <summary>
        /// Reflection with ReflCtx and a func argument requiring compilation
        /// </summary>
        MANUAL
    }

    public abstract class FileContext : IDisposable {
        protected readonly CookingContext parent;
        protected readonly KeyType keyType;
        private readonly object key;
        public FileContext(CookingContext parent, KeyType keyType, object key) {
            this.parent = parent;
            this.keyType = keyType;
            this.key = key;
        }

        public string FileIdentifier => string.Format("{0}{1}", keyType switch {
            KeyType.SM => "Sm",
            KeyType.SM_IMPORT => "Imp",
            KeyType.INTO => "Into",
            _ => "Manual"
        }, key.GetHashCode() + (long)int.MaxValue);

        public abstract void Dispose();

        public class GeneratedFunc {
            public string FnName { get; private set; }
            private static string BakeNameOfFn(string fnName) => fnName + "B";
            public string BakeName => BakeNameOfFn(FnName);
            public readonly string fnBody;
            public readonly Type retType;
            public readonly (Type typ, string argName)[] argDefs;
            public readonly object? originalValue;
            public ScriptFnDecl? CompileAsScriptFn { get; private set; }
            public bool CompileAsField { get; init; } = false;
            public bool CompileAsBake { get; init; } = true;
            
            public GeneratedFunc(string fnName, string fnBody, Type retType, (Type, string)[] argDefs, object? originalValue) {
                this.FnName = fnName;
                this.fnBody = fnBody;
                this.retType = retType;
                this.argDefs = argDefs;
                this.originalValue = originalValue;
            }

            public GeneratedFunc AsScriptFn(ScriptFnDecl? sfn) {
                if (sfn != null) {
                    FnName = ScriptFnCompiledName(sfn);
                    CompileAsScriptFn = sfn;
                }
                return this;
            }
            
            public static string ScriptFnCompiledName(ScriptFnDecl sfn) =>
                $"SFN{sfn.Name}{sfn.GetHashCode() + (long)int.MaxValue}";
            
            //GetAsConstant, except for recursive functions when the function has not been compiled yet
            public static string ScriptFnReference(ScriptFnDecl sfn) =>
                $"{BakeNameOfFn(ScriptFnCompiledName(sfn))}.{nameof(BakedExpr<int>.Func)}";

            public string GetAsConstant() {
                if (CompileAsBake)
                    return $"{BakeName}.{nameof(BakedExpr<int>.Func)}";
                if (CompileAsField)
                    return FnName;
                return $"{FnName}()";
            }

            public string PrintSFNRetType(Baker b) => 
                $"{nameof(BakedExpr<int>)}<{PrintRetType(b)}>";
            
            public string PrintRetType(Baker b) => b.TypePrinter.Print(retType);
            
            public string Print(Baker b) {
                if (!CompileAsField && CompileAsBake) {
                    return $@"
{PrintInner(b)}
private static {PrintSFNRetType(b)} {BakeName} = new({FnName});";
                }
                return PrintInner(b);
            }

            private string PrintInner(Baker b) {
                if (CompileAsField) {
                    if (!fnBody.StartsWith("return ") || argDefs.Length > 0)
                        throw new Exception($"Cannot compile {this} as a field");
                    return $@"
private static {PrintRetType(b)} {FnName} =
    {fnBody.Substring(7).Trim().Replace("\n", "\n\t")}";
                } else return $@"
private static {PrintRetType(b)} {FnName}(object[] args) {{
    {fnBody.Trim().Replace("\n", "\n\t")}
}}";
            }

            public string PrintAsEntry(Baker b) {
                var typ = CompileAsBake ? PrintSFNRetType(b) : 
                    "Func<" + string.Concat(argDefs.Select(ts => b.TypePrinter.Print(ts.typ) + ", ")) + PrintRetType(b) + ">";
                var name = CompileAsBake ? BakeName :
                    CompileAsField ? $"(() => {FnName})" : FnName;
                return $"({typ}){name}";
            }
        }
        
        /// <summary>
        /// A context that records generated functions in a file and eventually prints them to source code.
        /// </summary>
        public class Baker : FileContext {
            public bool DoNotExport { get; set; } = false;
            public ITypePrinter TypePrinter { get; set; } = new CSharpTypePrinter();
            public List<GeneratedFunc> GeneratedFunctions { get; } = new();

            public Baker(CookingContext parent, KeyType keyType, object key) : base(parent, keyType, key) { }
            public ExportedFile? Export() => (DoNotExport || GeneratedFunctions.Count == 0) ?
                (ExportedFile?)null :
                new ExportedFile(keyType, FileIdentifier, GeneratedFunctions.Where(gf => gf.CompileAsScriptFn != null), 
                    ExportFuncDefs(), GeneratedFunctions.Select(f => f.PrintAsEntry(this)));

            private IEnumerable<string> ExportFuncDefs() => GeneratedFunctions.Select(f => f.Print(this));

            private string MakeFuncName(string prefix, int index) => $"{prefix}_{index}";

            public void Add<D>(TExArgCtx tac, string fnBody, (Type, string)[] argDefs, object? origValue) {
                var name = MakeFuncName(FileIdentifier, GeneratedFunctions.Count);
                var fn = new GeneratedFunc(name, fnBody, typeof(D), argDefs, origValue)
                    { CompileAsField = tac.Ctx.CompileToField, 
                        CompileAsBake = !tac.Ctx.CompileToField }.AsScriptFn(tac.Ctx.CompileAsScriptFn);
                GeneratedFunctions.Add(fn);
            }

            public override void Dispose() {
                parent.DisposeBake(this);
            }
        }
        

        /// <summary>
        /// A proxy that retrieves functions from a source code file that was generated by Baker.
        /// </summary>
        public class Server : FileContext {
            private readonly List<object> compiled;
            private int index = 0;
        
            public Server(CookingContext parent, KeyType keyType, object key) : base(parent, keyType, key) {
                this.compiled = GeneratedExpressions.RetrieveBakedOrEmpty(FileIdentifier);
            }

            public D Next<D>(object[] proxyArgs) {
                if (index >= compiled.Count) {
                    if (compiled.Count == 0)
                        throw new Exception($"File {FileIdentifier} has no baked expressions, but one was requested. This probably means you changed the file contents after baking the expressions.");
                    throw new Exception($"Not enough baked expressions for file {FileIdentifier}");
                }
                var func = compiled[index++];
                if (func is BakedExpr<D> bsfn)
                    return bsfn.Load(proxyArgs);
                var invoker = func.GetType().GetMethod("Invoke")!;
                var obj = invoker.Invoke(func, proxyArgs);
                if (obj is D del) 
                    return del;
                throw new Exception($"Baked expression #{index}/{compiled.Count} for file {FileIdentifier} " +
                                    $"is of type {obj.GetType().SimpRName()}, requested {typeof(D).SimpRName()}");
            }
        
            public override void Dispose() {
                parent.DisposeServe(this);
            }
        }
        
    }
}

}