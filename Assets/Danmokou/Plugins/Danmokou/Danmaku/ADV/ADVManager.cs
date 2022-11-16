using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.VN;
using Newtonsoft.Json;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;

namespace Danmokou.ADV {

/// <summary>
/// Service that manages the execution of an ADV context.
/// </summary>
public class ADVManager : CoroutineRegularUpdater {
    public enum State {
        Investigation = 0,
        Dialogue = 100,
        Waiting = 200
    }

    public static ADVReferences ADVReferences => GameManagement.References.advReferences!;
    public ADVData ADVData => ExecAdv!.ADVData;
    public DMKVNState VNState => ExecAdv!.VN;
    public IExecutingADV? ExecAdv { get; private set; }
    public DisturbedEvented<State> ADVState { get; } = new DisturbedFold<State>(State.Investigation, 
        (x, y) => (x > y) ? x : y);

    protected override void BindListeners() {
        RegisterService(this);
        Listen(ADVState, s => Logs.Log($"The running ADV state is now {s}."));
    }

    public void DestroyCurrentInstance() {
        ExecAdv?.Inst.Cancel();
    }
    
    /// <summary>
    /// Set the provided ADV execution as the current executing ADV.
    /// <br/>(Only one <see cref="IExecutingADV"/> may be handled by this service at a time.)
    /// </summary>
    public void SetupInstance(IExecutingADV inst) {
        DestroyCurrentInstance();
        ExecAdv = inst;
    }

    public bool RunCampaign(ADVGameDef gameDef, ADVData advData) =>
        new ADVInstanceRequest(this, gameDef, advData).Run(GameManagement.References.mainMenu) is { };

    /// <summary>
    /// Restart the currently executing game with a different <see cref="ADVData"/>.
    /// </summary>
    public bool Restart(ADVData advData) => 
        ExecAdv.Try(out var exec) && RunCampaign(exec.Inst.Request.Game, advData);


    public ADVData GetSaveReadyADVData() {
        VNState.UpdateInstanceData();
        //If saving within an unlocateable VN execution, then use the unmodified save data for safety
        if (VNState.InstanceData.Location is null && ADVData.UnmodifiedSaveData is not null) {
            return ADVData.GetUnmodifiedSaveData() ?? throw new Exception("Couldn't load unmodified save data");
        }
        return ADVData;
    }

    public Task<T>? TryExecuteVN<T>(BoundedContext<T> task, bool allowParallelInvestigation = false) {
        VNState.Flush();
        if (VNState.Contexts.Count > 0)
            return null;
        return ExecuteVN(task, allowParallelInvestigation);
    }

    /// <summary>
    /// Execute a top-level VN segment.
    /// </summary>
    public async Task<T> ExecuteVN<T>(BoundedContext<T> task, bool allowParallelInvestigation = false) {
        var vn = VNState;
        vn.Flush();
        if (vn.Contexts.Count > 0)
            throw new Exception($"Executing a top-level VN segment {task.ID} when one is already active");
        if (ADVData.UnmodifiedSaveData != null)
            throw new Exception($"Executing a top-level VN segment {task.ID} when unmodifiedSaveData is non-null");
        vn.ResetInterruptStatus();
        var inst = ExecAdv ?? throw new Exception();
        (VNState.MainDialogue as ADVDialogueBox)?.MinimalState.PublishIfNotSame(allowParallelInvestigation);
        using var _ = ADVState.AddConst(allowParallelInvestigation ? State.Investigation : State.Dialogue);
        Logs.Log($"Starting VN segment {task.ID}");
        ADVData.UnmodifiedSaveData = FileUtils.SerializeJson(ADVData, Formatting.None);
        try {
            var res = await task;
            vn.UpdateInstanceData();
            return res;
        } catch (Exception e) {
            if (e is OperationCanceledException)
                Logs.Log($"Cancelled VN segment {task.ID}");
            else
                Logs.LogException(e);
            throw;
        } finally {
            vn.ResetInterruptStatus();
            ADVData.UnmodifiedSaveData = null;
            Logs.Log($"Completed VN segment {task.ID}. Final state: {inst.Inst.Tracker.ToCompletion()}");
            //TODO: require a smarter way to handle "reverting to previous state"
        }
    }
}
}