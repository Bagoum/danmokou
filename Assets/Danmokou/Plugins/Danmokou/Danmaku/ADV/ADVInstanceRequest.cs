using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using SuzunoyaUnity;

namespace Danmokou.ADV {
/// <summary>
/// Contains all top-level metadata about an executing ADV instance that is not specific to the game.
/// <br/>The actual execution process is handled by a game-specific <see cref="IExecutingADV"/>.
/// </summary>
/// <param name="Request"></param>
/// <param name="VN"></param>
/// <param name="eVN">Unity wrapper around <see cref="VN"/></param>
/// <param name="Tracker">Cancellation token wrapping the ADV instance execution.</param>
public record ADVInstance(ADVInstanceRequest Request, DMKVNState VN, ExecutingVN eVN, Cancellable Tracker) : IDisposable { 
    /// <inheritdoc cref="ADVInstanceRequest.ADVData"/>
    public ADVData ADVData => Request.ADVData;
    /// <inheritdoc cref="ADVInstanceRequest.Manager"/>
    public ADVManager Manager => Request.Manager;
    public void Cancel() {
        Tracker.Cancel();
        VN.DeleteAll(); //this cascades into destroying executingVN
    }

    public void Dispose() => Cancel();
}

/// <summary>
/// Contains information necessary to start an ADV instance.
/// <br/>Once the instance is started, metadata such as the execution tracker
/// is stored in a constructed <see cref="ADVInstance"/>.
/// </summary>
public class ADVInstanceRequest {
    /// <inheritdoc cref="ADVManager"/>
    public ADVManager Manager { get; }
    /// <summary>
    /// Game definition.
    /// </summary>
    public ADVGameDef Game { get; }
    /// <summary>
    /// Save data to load from.
    /// </summary>
    public ADVData ADVData { get; private set; }
    /// <summary>
    /// During loading, this contains the "true" save data,
    ///  that is replayed onto the "blank" save data in <see cref="ADVData"/>.
    /// </summary>
    public ADVData? LoadProxyData { get; private set; }
    
    public ADVInstanceRequest(ADVManager manager, ADVGameDef game, ADVData advData) {
        Manager = manager;
        Game = game;
        (ADVData, LoadProxyData) = advData.GetLoadProxyInfo();
    }

    public void FinalizeProxyLoad() {
        if (LoadProxyData == null)
            throw new Exception($"{nameof(FinalizeProxyLoad)} called when no proxy data exists");
        Logs.Log($"Finished loading ADV instance");
        if (LoadProxyData != null) {
            ADVData = LoadProxyData;
            LoadProxyData = null;
        }
    }

    /// <summary>
    /// Enter the ADV scene and run the ADV instance.
    /// Returns null if the scene fails to load.
    /// </summary>
    public Task<IADVCompletion>? Run(SceneConfig? returnTo) {
        if (returnTo == null)
            returnTo = GameManagement.References.mainMenu;
        //You can start running this before the curtain pulls back, so we use TaskFromOnLoad instead of TaskFromOnFinish
        if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnLoad(
                Game.sceneConfig,
                SceneRequest.Reason.START_ONE,
                Manager.DestroyCurrentInstance,
                RunInScene,
                null,
                out var tcs)) is null)
            return null;
        async Task<IADVCompletion> Rest() {
            var (result, dispose) = await tcs.Task;
            if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(
                    new SceneRequest(returnTo,
                        SceneRequest.Reason.FINISH_RETURN,
                        dispose.Dispose
                    )) is { } loader) {
                await loader.Finishing.Task;
            } else
                throw new Exception("Couldn't return to menu after ADV completion");
            return result;
        }
        return Rest();
    }

    /// <summary>
    /// Run the ADV instance in the current scene. 
    /// </summary>
#if UNITY_EDITOR
    public 
#else
    private
#endif
    async Task<(IADVCompletion result, IDisposable cleanup)> RunInScene() {
        var Tracker = new Cancellable();
        var vn = new DMKVNState(Tracker, Game.Key, ADVData.VNData);
        var evn = ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
        ServiceLocator.Find<IVNBacklog>().TryRegister(evn);
        if (Game.backlogFeatures == ADVBacklogFeatures.ALLOW_BACKJUMP)
            evn.doBacklog = loc => {
                vn.UpdateInstanceData().Location = loc;
                Manager.Restart(ADVData);
            };
        var inst = new ADVInstance(this, vn, evn, Tracker);
        using var exec = Game.Setup(inst);
        Manager.SetupInstance(exec);
        var result = await exec.Run();
        return (result, inst);
    }
}
}