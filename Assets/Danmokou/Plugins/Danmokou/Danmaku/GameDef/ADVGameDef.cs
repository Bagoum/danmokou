using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.VN;
using Suzunoya.ControlFlow;
using UnityEngine;

namespace Danmokou.ADV {

public enum ADVBacklogFeatures {
    NONE,
    /// <summary>
    /// True iff the player should be allowed to go to a previous
    ///  point in the game by using the dialogue log menu.
    /// <br/>This should only be allowed for linear games.
    /// </summary>
    ALLOW_BACKJUMP
}

/// <summary>
/// A wrapper ScriptableObject subclassed to define functionality for an ADV game.
/// <br/>Most actual code in subclasses occurs in a nested class implementing <see cref="IExecutingADV"/>
///  that is returned by <see cref="Setup"/>.
/// </summary>
public abstract class ADVGameDef : GameDef {
    public SceneConfig sceneConfig = null!;
    public ADVBacklogFeatures backlogFeatures = ADVBacklogFeatures.NONE;
    
    /// <summary>
    /// Create the game-specific execution process for this ADV game.
    /// </summary>
    /// <param name="inst">Instance metadata</param>
    public abstract IExecutingADV Setup(ADVInstance inst);
    public abstract ADVData NewGameData();
}
}