using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;

namespace Danmokou.ADV {

public class ADVManager : CoroutineRegularUpdater {
    public ADVData ADVData => instance!.ADVData;
    public DMKVNState VNState => instance!.VN;
    private ADVInstance? instance;
    public bool IsExecuting => instance?.Tracker.Cancelled is false;

    protected override void BindListeners() {
        ServiceLocator.Register(this);
    }

    public void DestroyCurrentInstance() {
        instance?.Cancel();
    }
    public void SetupInstance(ADVInstance inst) {
        DestroyCurrentInstance();
        instance = inst;
    }

    public bool RunCampaign(ADVGameDef gameDef, ADVData advData) =>
        new ADVInstanceRequest(this, gameDef, advData).Run();

    /// <summary>
    /// Restart the currently executing game with a different <see cref="ADVData"/>.
    /// </summary>
    public bool Restart(ADVData advData) => 
        instance.Try(out var inst) && RunCampaign(inst.Request.Game, advData);


    public ADVData GetSaveReadyADVData() {
        VNState.UpdateSavedata();
        return ADVData;
    }

    public async Task<T> ExecuteVN<T>(Func<DMKVNState, Task<T>> task) {
        var vn = VNState;
        var inst = instance ?? throw new Exception();
        Logs.Log($"Starting VN segment {vn}");
        try {
            var res = await task(vn);
            vn.UpdateSavedata();
            return res;
        } catch (Exception e) {
            if (e is OperationCanceledException)
                Logs.Log($"Cancelled VN segment");
            else
                Logs.LogException(e);
            throw;
        } finally {
            Logs.Log($"Completed VN segment. Final state: {inst.Tracker.ToCompletion()}");
        }
    }
}
}