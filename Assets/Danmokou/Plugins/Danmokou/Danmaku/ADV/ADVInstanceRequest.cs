using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Services;
using Danmokou.VN;
using SuzunoyaUnity;

namespace Danmokou.ADV {
/// <summary>
/// Contains all top-level metadata about an executing ADV instance.
/// <br/>The actual execution process is handled by a game-specific <see cref="IExecutingADV"/>.
/// </summary>
public record ADVInstance(ADVInstanceRequest Request, DMKVNState VN, ExecutingVN eVN, Cancellable Tracker) { 
    public ADVData ADVData => Request.ADVData;
    public ADVManager Manager => Request.Manager;
    public void Cancel() {
        Tracker.Cancel();
        VN.DeleteAll(); //this cascades into destroying executingVN
    }

    
    public bool Finish() {
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            new SceneRequest(GameManagement.References.mainMenu,
                SceneRequest.Reason.FINISH_RETURN,
                VN.DeleteAll
                ));
    }
}

/// <summary>
/// Contains information necessary to start an ADV instance.
/// <br/>Once the instance is started, metadata such as the execution tracker
/// is stored in a constructed <see cref="ADVInstance"/>.
/// </summary>
public class ADVInstanceRequest {
    public ADVManager Manager { get; }
    public ADVGameDef Game { get; }
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
    /// </summary>
    public bool Run() {
        return ServiceLocator.Find<ISceneIntermediary>().LoadScene(new SceneRequest(
            Game.sceneConfig,
            SceneRequest.Reason.START_ONE,
            Manager.DestroyCurrentInstance,
            () => _ = RunInScene().ContinueWithSync()));
    }

    /// <summary>
    /// Run the ADV instance in the current scene. 
    /// </summary>
#if UNITY_EDITOR
    public 
#else
    private
#endif
        async Task RunInScene() {
        var Tracker = new Cancellable();
        var vn = new DMKVNState(Tracker, Game.key, ADVData.VNData);
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
        //You can start running this before the curtain pulls back.
        await exec.Run().ContinueWithSync(Tracker.Guard(() => inst.Finish()));
    }
}
}