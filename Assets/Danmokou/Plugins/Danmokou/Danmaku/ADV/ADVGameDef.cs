using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.ADV {

public abstract class ADVGameDef : ScriptableObject {
    public string key = "";
    public SceneConfig sceneConfig = null!;
    public bool allowVnBackjump = true;


    public abstract Task Run(ADVInstance inst);
    public abstract ADVData NewGameData();
}
}