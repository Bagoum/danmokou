using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Tasks;
using Suzunoya.ADV;
using SuzunoyaUnity;
using UnityEngine;

namespace SZYU.Examples {

public class ExampleADVInstanceRequest : IADVInstanceRequest {
    public ADVManager Manager { get; }
    public ADVData ADVData { get; private set; }
    public ADVData? LoadProxyData { get; private set; }
    private Func<ADVInstance, IExecutingADV> executor;
    public ExampleADVInstanceRequest(ADVManager manager, ADVData advData, Func<ADVInstance, IExecutingADV> executor) {
        Manager = manager;
        this.executor = executor;
        (ADVData, LoadProxyData) = advData.GetLoadProxyInfo();
    }

    public void FinalizeProxyLoad() {
        if (LoadProxyData == null)
            throw new Exception($"{nameof(FinalizeProxyLoad)} called when no proxy data exists");
        Debug.Log($"Finished loading ADV instance");
        if (LoadProxyData != null) {
            ADVData = LoadProxyData;
            LoadProxyData = null;
        }
    }

    public bool Run() {
        _ = _Run().ContinueWithSync();
        return true;
    }

    private async Task _Run() {
        var tracker = new Cancellable();
        var vn = new UnityVNState(tracker, ADVData.VNData);
        ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
        var inst = new ADVInstance(this, vn, tracker);
        using var exec = executor(inst);
        Manager.SetupInstance(exec);
        var result = await exec.Run();
        inst.Dispose();
    }
    
    public bool Restart(ADVData? data = null) {
        throw new NotImplementedException();
    }
}

}