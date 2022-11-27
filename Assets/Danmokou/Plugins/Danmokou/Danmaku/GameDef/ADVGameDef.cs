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
using Suzunoya.ADV;
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
/// Game definition for ADV games.
/// </summary>
public interface IADVGameDef : IGameDef {
    SceneConfig Scene { get; }
    ADVBacklogFeatures BacklogFeatures { get; }
    
    ADVData NewGameData();
    
    /// <summary>
    /// Create the game-specific execution process for this ADV game.
    /// </summary>
    /// <param name="inst">Instance metadata</param>
    public abstract IExecutingADV Setup(ADVInstance inst);
}

/// <summary>
/// A wrapper ScriptableObject subclassed to define functionality for an ADV game.
/// <br/>Most actual code in subclasses occurs in a nested class implementing <see cref="IExecutingADV"/>
///  that is returned by <see cref="Setup"/>.
/// </summary>
public abstract class ADVGameDef : GameDef, IADVGameDef {
    public SceneConfig sceneConfig = null!;
    public ADVBacklogFeatures backlogFeatures = ADVBacklogFeatures.NONE;
    
    /// <summary>
    /// Instantiate a process responsible for running the ADV game.
    /// <br/>Note: This process is responsible for updating itself if required (via <see cref="IRegularUpdater"/>),
    /// <see cref="IExecutingADV"/> implements <see cref="IRegularUpdater"/>, this process i
    /// </summary>
    /// <param name="inst">Information about the execution context.</param>
    public abstract IExecutingADV Setup(ADVInstance inst);
    public abstract ADVData NewGameData();

    public SceneConfig Scene => sceneConfig;
    public ADVBacklogFeatures BacklogFeatures => backlogFeatures;
}


}