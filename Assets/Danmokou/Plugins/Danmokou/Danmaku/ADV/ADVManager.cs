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
/// <br/>Note that this is not involved in the execution of VN contexts
/// </summary>
public class ADVManager : CoroutineRegularUpdater {
    public enum State {
        Investigation = 0,
        Dialogue = 100,
        Waiting = 200
    }

    public static ADVReferences ADVReferences => GameManagement.References.advReferences!;
    public ADVData ADVData => instance!.ADVData;
    public DMKVNState VNState => instance!.VN;
    private ADVInstance? instance;
    public DisturbedEvented<State> ADVState { get; } = new DisturbedFold<State>(State.Investigation, 
        (x, y) => (x > y) ? x : y);

    protected override void BindListeners() {
        RegisterService(this);
        Listen(ADVState, s => Logs.Log($"The running ADV state is now {s}."));
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
        VNState.UpdateInstanceData();
        return ADVData;
    }

    /// <summary>
    /// Execute a top-level VN segment.
    /// </summary>
    public async Task<T> ExecuteVN<T>(BoundedContext<T> task, State statePhase = State.Dialogue) {
        var vn = VNState;
        vn.Flush();
        if (vn.Contexts.Count > 0)
            throw new Exception("Executing a top-level VN segment when one is already active");
        if (ADVData.UnmodifiedSaveData != null)
            throw new Exception("Executing a top-level VN segment when unmodifiedSaveData is non-null");
        var inst = instance ?? throw new Exception();
        using var _ = ADVState.AddConst(statePhase);
        Logs.Log($"Starting VN segment {vn}");
        ADVData.UnmodifiedSaveData = FileUtils.SerializeJson(ADVData, Formatting.None);
        try {
            var res = await task;
            vn.UpdateInstanceData();
            return res;
        } catch (Exception e) {
            if (e is OperationCanceledException)
                Logs.Log($"Cancelled VN segment: {e}");
            else
                Logs.LogException(e);
            throw;
        } finally {
            ADVData.UnmodifiedSaveData = null;
            Logs.Log($"Completed VN segment. Final state: {inst.Tracker.ToCompletion()}");
            //TODO: require a smarter way to handle "reverting to previous state"
        }
    }
}
}