using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "bdsl")]
public class BDSLExtensionImporter : ScriptedImporter {
    public override void OnImportAsset(AssetImportContext ctx) {
        var subAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
        ctx.AddObjectToAsset("text", subAsset);
        ctx.SetMainObject(subAsset);
    }
}