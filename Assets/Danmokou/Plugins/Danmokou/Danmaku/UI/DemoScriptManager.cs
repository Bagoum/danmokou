using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Reflection;
using Danmokou.Services;
using Danmokou.SM;
using Scriptor.Compile;
using TMPro;
using UnityEngine;


namespace Danmokou.UI {
public class DemoScriptManager : MonoBehaviour {
    public TextMeshPro scriptDisplay = null!;
    public Transform buttonGroup = null!;
    public GameObject buttonPrefab = null!;
    public NamedTextAsset[] nscripts = null!;
    private string?[] displayTexts = null!;
    public BehaviorEntity boss = null!;
    private Regex comment = new("//.*");


    public void RequestScript(NamedTextAsset script) {
        GameManagement.ClearPhaseAutocull(PhaseProperties.DefaultSoftcullProps(boss),
            PhaseProperties.DefaultBehSoftcullProps(boss));
        boss.RunBehaviorSM(SMRunner.RunRoot(script.file, Cancellable.Null)).Log();
        ref var displayTex = ref displayTexts[nscripts.IndexOf(script)];
        scriptDisplay.text = displayTex ??= Highlight(script.file.text);
    }

    public void Start() {
        RequestScript(nscripts[0]);
    }
    
    public void Awake() {
        //scriptDisplay.richText = false;
        foreach (var s in nscripts) {
            Instantiate(buttonPrefab, buttonGroup)
                .GetComponent<DemoScriptButton>()
                .Initialize(s, this, s.name);
        }
        displayTexts = new string?[nscripts.Length];
    }

    private string Highlight(string text) {
        var sb = new StringBuilder();
        var ii = 0;
        var (ast, gs) = CompileHelpers.ParseAnnotate(ref text);
        ast = ast.Typecheck(gs, null, out _).Finalize();
        foreach (var t in ast.ToSemanticTokens().OrderBy(t => t.Position.Start.Index)) {
            while (ii < t.Position.Start.Index)
                sb.Append(text[ii++]);
            var color = Color(t);
            if (color != null)
                sb.Append($"<color=#{color}>");
            while (ii < t.Position.End.Index)
                sb.Append(text[ii++]);
            if (color != null)
                sb.Append("</color>");
        }
        while (ii < text.Length)
            sb.Append(text[ii++]);
        return comment.Replace(sb.ToString().Replace("\t", "  "), m => $"<color=#6a9955>{m.Value}</color>");
    }

    private string? Color(SemanticToken t) {
        var typ = t.TokenType;
        if (t.TokenMods != null) {
            var sb = new StringBuilder();
            foreach (var mod in t.TokenMods) {
                if (mod is not "static") {
                    sb.Append('.');
                    sb.Append(mod);
                }
            }
            typ += sb.ToString();
        }
        return typ switch {
            "variable.dmkdynamicvar" => "7daef3",
            "dmkOperator" => "c0c0c0",
            "dmkEnumMember" => "e5813b",
            "function" => "ff87bd",
            "variable.const" => "b5cea8",
            "method.dmksm" => "7b8be6",
            "method.dmkasyncpattern" => "f24848",
            "method.dmksyncpattern" => "d8c020",
            "method.dmksyncpattern.dmkatomic" => "e2a624",
            "method.dmkcontrols" => "3f7ce5",
            "method.dmkproperties" => "44b4ff",
            "method.dmkbpy" => "9c6df5",
            "method.dmkbpy.dmkatomic" => "afb4e5",
            "method.dmktp4" => "c64b42",
            "method.dmkbprv2" => "c64b42",
            "method.dmktp3" => "ff6e42",
            "method.dmktp" => "c656d5",
            "method.dmkvtp" => "ed4c8f",
            "method.dmkpred" => "7ade7f",
            "comment" => "6a9955",
            "string" => "ce9178",
            "keyword" => "C586C0",
            "variable" => "9CDCFE",
            "number" => "b5cea8",
            "type" => "4EC9B0",
            _ => null
        };
    }
}
}