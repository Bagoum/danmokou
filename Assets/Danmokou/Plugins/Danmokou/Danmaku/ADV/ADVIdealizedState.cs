using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using Danmokou.VN;
using Suzunoya.ControlFlow;

namespace Danmokou.ADV {
public enum RunOnEntryVNPriority {
    /// <summary>
    /// Run a VN on entry because the game was saved while that VN was running.
    /// </summary>
    LOAD = 0,
    /// <summary>
    /// Run a VN on entry because the map configuration says so.
    /// </summary>
    MAP_ENTER = 1
}
public record ADVIdealizedState : IdealizedState {
    private IExecutingADV inst;
    private (BoundedContext<Unit> bctx, RunOnEntryVNPriority priority)? runOnEnterVN;
    public bool HasEntryVN => runOnEnterVN != null;
    
    public ADVIdealizedState(IExecutingADV inst) {
        this.inst = inst;
        Assert(new RunOnEntryAssertion(EntryTask) {
            Priority = (int.MaxValue, int.MaxValue)
        });
    }

    public bool SetEntryVN(BoundedContext<Unit> bctx, RunOnEntryVNPriority priority = RunOnEntryVNPriority.MAP_ENTER) {
        if (runOnEnterVN == null || runOnEnterVN?.priority > priority) {
            runOnEnterVN = (bctx, priority);
            return true;
        }
        return false;
    }

    private Task EntryTask() {
        if (runOnEnterVN.Try(out var x)) {
            //Don't await this! We only want to start the bctx--
            // it will be completed during normal play.
            _ = inst.Manager.ExecuteVN(x.bctx);
        }
        return Task.CompletedTask;
    }
    
    //TODO: extend these with orderings for background, music, etc.
    public override async Task ActualizeOnNewState() {
        await base.ActualizeOnNewState();
        await FadeIn();
    }

    public override async Task DeactualizeOnEndState() {
        await FadeOut();
        await base.DeactualizeOnEndState();
        inst.VN.MainDialogue?.Clear();
    }

    protected virtual Task FadeIn() => Task.CompletedTask;
    protected virtual Task FadeOut() => Task.CompletedTask;
}
}