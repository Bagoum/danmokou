using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.Dialogue;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.Data;
using SuzunoyaUnity;
using static BagoumLib.Tasks.WaitingUtils;

namespace Danmokou.SM {
/// <summary>
/// `script`: Top-level controller for dialogue files.
/// </summary>
public class ScriptTSM : SequentialSM {
    public ScriptTSM(List<StateMachine> states) : base(states) {}

    public override async Task Start(SMHandoff smh) {
        using var token = ServiceLocator.FindAll<PlayerController>()
            .SelectDisposable(p => p.AllControlEnabled.AddConst(false));
        //Await to keep token in scope until exit
        await ((DMKVNWrapper) ServiceLocator.Find<IVNWrapper>())
            .ExecuteVN((data, cT) => new DMKVNState(cT, "backwards-compat-script-vn", data),
                vn => RunAsVN(smh, vn), new InstanceData(new GlobalData()), smh.cT);

        //Dialoguer.ShowAndResetDialogue();
        //return base.Start(smh).ContinueWithSync(Dialoguer.HideDialogue);
    }

    private async Task RunAsVN(SMHandoff smh, DMKVNState vn) {
        using var smh2 = new SMHandoff(smh, vn.CToken);
        vn.DefaultRenderGroup.Priority.Value = 100;
        var md = vn.Add(new ADVDialogueBox());
        _ = md.FadeTo(1, 0.5f).Task;
        try {
            await base.Start(smh2);
        } catch (OperationCanceledException) when (!smh.Cancelled) {
            //Don't throw upwards if the VN was cancelled locally
        }
    }
}
public abstract class ScriptLineSM : StateMachine {}
public class ReflectableSLSM : ScriptLineSM {
    private readonly TTaskPattern func;
    public ReflectableSLSM(TTaskPattern func) {
        this.func = func;
    }
    public override Task Start(SMHandoff smh) => func(smh);
}

}
