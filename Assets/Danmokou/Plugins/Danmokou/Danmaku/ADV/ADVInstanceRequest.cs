using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya.ADV;
using Suzunoya.ControlFlow;
using SuzunoyaUnity;

namespace Danmokou.ADV {

/// <inheritdoc/>
public class ADVInstanceRequest : IADVInstanceRequest {
    /// <inheritdoc cref="ADVManagerWrapper"/>
    public ADVManager Manager { get; }
    /// <summary>
    /// Game definition.
    /// </summary>
    public IADVGameDef Game { get; }
    /// <summary>
    /// Save data to load from.
    /// </summary>
    public ADVData ADVData { get; private set; }
    /// <summary>
    /// During loading, this contains the "true" save data,
    ///  that is replayed onto the "blank" save data in <see cref="ADVData"/>.
    /// </summary>
    public ADVData? LoadProxyData { get; private set; }

    private Action<IADVCompletion, IDisposable> Finalize { get; }

    public ADVInstanceRequest(ADVManager manager, IADVGameDef game, ADVData advData,
        Action<IADVCompletion, IDisposable>? finalize = null) {
        Manager = manager;
        Game = game;
        this.Finalize = finalize ?? ((a, b) => DefaultReturn(a, b));
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
    public bool Run() {
        //You can start running this before the curtain pulls back, so we use TaskFromOnLoad instead of TaskFromOnFinish
        if (ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnLoad(
                Game.Scene,
                SceneRequest.Reason.START_ONE,
                Manager.DestroyCurrentInstance,
                RunInScene,
                null,
                out var tcs)) is null)
            return false;
        _ = tcs.Task.ContinueSuccessWithSync(result => Finalize(result.result, result.cleanup));
        return true;
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
        if (Game.BacklogFeatures != ADVBacklogFeatures.NO_BACKLOG)
            ServiceLocator.Find<IVNBacklog>().TryRegister(evn);
        if (Game.BacklogFeatures == ADVBacklogFeatures.ALLOW_BACKJUMP)
            evn.doBacklog = loc => {
                vn.UpdateInstanceData().Location = loc;
                Restart();
            };
        var inst = new ADVInstance(this, vn, Tracker);
        using var exec = Game.Setup(inst);
        Manager.SetupInstance(exec);
        var result = await exec.Run();
        Logs.Log($"ADV execution for {Game.Key} is complete with result {result}");
        return (result, inst);
    }

    public bool Restart(ADVData? data = null) {
        return new ADVInstanceRequest(Manager, Game, data ?? ADVData, Finalize).Run();
    }

    public static bool DefaultReturn(IADVCompletion result, IDisposable dispose) =>
        ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            new SceneRequest(GameManagement.References.mainMenu,
                SceneRequest.Reason.FINISH_RETURN,
                dispose.Dispose
            )) is { } loader;
}
}