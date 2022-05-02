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
public class ADVInstanceRequest {
    public ADVManager Manager { get; }
    public ADVGameDef Game { get; }
    public ADVData ADVData { get; private set; }
    public ADVData? LoadProxyData { get; private set; }
    
    public ADVInstanceRequest(ADVManager manager, ADVGameDef game, ADVData advData) {
        Manager = manager;
        Game = game;
        (ADVData, LoadProxyData) = 
            Game.backlogFeatures == ADVBacklogFeatures.USE_PROXY_LOADING ?
                advData.GetLoadProxyInfo() :
                (advData, null);
    }

    public void FinalizeProxyLoad() {
        Logs.Log($"Finished loading ADV instance (will swap proxy data: " +
                 $"{Game.backlogFeatures == ADVBacklogFeatures.USE_PROXY_LOADING})");
        if (Game.backlogFeatures == ADVBacklogFeatures.USE_PROXY_LOADING && LoadProxyData != null) {
            ADVData = LoadProxyData;
            LoadProxyData = null;
        }
    }

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
        //Use proxy data for the VN execution, but use cleanslate data for map configuration
        var vn = new DMKVNState(Tracker, Game.key, (LoadProxyData ?? ADVData).VNData);
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