using System;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using Danmokou.Core;
using SuzunoyaUnity;
using UnityEngine;
using Log = Danmokou.Core.Log;

namespace Danmokou.VN {
public abstract class VNBaseScript {
    protected virtual UnityVNState MakeVN(ICancellee cT) => new UnityVNState(cT);

    public async Task RunScript(ICancellee? extCT = null) {
        Log.Unity($"Started script {this}");
        var cT = new Cancellable();
        var jcT = new JointCancellee(cT, extCT);
        var vn = MakeVN(jcT);
        DependencyInjection.Find<IVNWrapper>().TrackVN(vn);
        try {
            await vn.Wait(0f);
            await _RunScript(vn);
        } catch (Exception e) {
            if (e is OperationCanceledException) {
                Log.Unity($"VN object {this} has been cancelled");
            } else {
                Log.UnityException(e);
            }
            throw;
        } finally {
            Log.Unity($"Done with running script {this}. Final state: Local {cT.ToCompletion()}, total {jcT.ToCompletion()}");
            cT.Cancel();
            vn.DeleteAll();
        }
    }

    protected abstract Task _RunScript(UnityVNState vn);
}
}