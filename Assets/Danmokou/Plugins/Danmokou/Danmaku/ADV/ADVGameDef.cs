using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.ADV {

public enum ADVBacklogFeatures {
    NONE,
    /// <summary>
    /// True iff the player should be allowed to go to a previous
    ///  point in the game by using the dialogue log menu.
    /// <br/>This should only be allowed for linear games.
    /// </summary>
    ALLOW_BACKJUMP,
    /// <summary>
    /// True iff proxy loading should be enabled.
    /// <br/>Proxy loading saves an "unmodified" copy of the save data when
    /// entering a top-level BCtx, and uses that unmodified copy to execute
    /// the loading process.
    /// </summary>
    USE_PROXY_LOADING
}
public abstract class ADVGameDef : ScriptableObject {
    public string key = "";
    public SceneConfig sceneConfig = null!;
    public ADVBacklogFeatures backlogFeatures = ADVBacklogFeatures.USE_PROXY_LOADING;


    public abstract Task Run(ADVInstance inst);
    public abstract ADVData NewGameData();
}
}