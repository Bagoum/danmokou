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
/// This code is maintained for backwards compatibility; please use the new VN interfaces instead.
/// </summary>
public class ScriptTSM : SequentialSM {
    public ScriptTSM([BDSL1ImplicitChildren] StateMachine[] states) : base(states) {}

    public override Task Start(SMHandoff smh) =>
        SMReflection.ExecuteVN(vn => RunAsVN(smh, vn), "backwards-compat-script-vn").Start(smh);

    private async Task RunAsVN(SMHandoff smh, DMKVNState vn) {
        using var smh2 = new SMHandoff(smh, vn.CToken);
        vn.DefaultRenderGroup.Priority.Value = 100;
        using var md = vn.Add(new ADVDialogueBox());
        //_ = md.FadeTo(1, 0.5f).Task;
        vn.DefaultRenderGroup.Alpha = 0.2f;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        await base.Start(smh2);
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
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
