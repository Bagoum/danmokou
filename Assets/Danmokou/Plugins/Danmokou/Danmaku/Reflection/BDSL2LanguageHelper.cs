using System;
using System.Collections;
using System.Linq;
using System.Text;
using BagoumLib;
using BagoumLib.Reflection;
using Danmokou.Behavior;
using Danmokou.Core;
using Scriptor.Analysis;
using Scriptor.Compile;
using Scriptor.Definition;
using Scriptor.Reflection;
using UnityEngine;

namespace Danmokou.Reflection {
public class BDSL2LanguageHelper : CoroutineRegularUpdater {
    public TextAsset? loadScript;
    public string targetType = "float";
    
    public override void FirstFrame() {
        ParseScript();
    }

    [ContextMenu("Parse script")]
    public void ParseScript() {
        if (loadScript != null) {
            var rtyp = LangParser.TypeFromString(this.targetType);
            if (!rtyp.TryL(out var typ))
                Logs.Log($"`{targetType}` is not a valid type identifier.", level: LogLevel.ERROR);
            if (typ == null! || typ == typeof(void))
                DebugErasedFromText(loadScript.text);
            else
                debugScript.Specialize(typ).Invoke(loadScript.text);
        }
    }

    private delegate int MyDelegateType(int a, int b, out EnvFrame ef);
    public void ExampleCustomDelegateCompilation() {
        var fn1 = CompileHelpers.ParseAndCompileDelegate<Func<int, int, int>>("a + b", 
            new DelegateArg<int>("a"),
            new DelegateArg<int>("b"));
        Debug.Log(fn1(120, 1004));
        var fn2 = CompileHelpers.ParseAndCompileDelegate<MyDelegateType>(@"
var c = a * 2;
function incrementC():: void {
    c++;
}
incrementC();
b + c;", 
            new DelegateArg<int>("a"),
            new DelegateArg<int>("b"),
            CompileHelpers.OutEnvFrameArg);
        Debug.Log(fn2(120, 1004, out var ef));
        var sb = new StringBuilder();
        DebugHelpers.DebugRootEf(sb, ef);
        Debug.Log(sb.ToString());
    }

    public static void DebugFromText<T>(string text) {
        var (result, ef) = CompileHelpers.ParseAndCompileValue<T>(text);
        var sb = new StringBuilder();
        sb.Append($"{nameof(BDSL2LanguageHelper)} parsed a script with a result of: {DebugHelpers.Print(result)} " +
                 $"({typeof(T).RName()}).\n");
        DebugHelpers.DebugRootEf(sb, ef);
        Logs.Log(sb.ToString());
    }
    
    public static void DebugErasedFromText(string text) {
        var ef = CompileHelpers.ParseAndCompileErased(text);
        var sb = new StringBuilder();
        sb.Append($"{nameof(BDSL2LanguageHelper)} parsed a script with a void result.\n");
        DebugHelpers.DebugRootEf(sb, ef);
        Logs.Log(sb.ToString());
    }

    private static readonly GenericMethodSignature debugScript =
        (GenericMethodSignature)MethodSignature.Get(typeof(BDSL2LanguageHelper).GetMethod(nameof(DebugFromText))!);

}
}