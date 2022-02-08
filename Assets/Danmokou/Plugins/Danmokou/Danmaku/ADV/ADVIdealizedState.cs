using System.Threading.Tasks;
using BagoumLib.Assertions;
using Danmokou.VN;

namespace Danmokou.ADV {
public abstract record ADVIdealizedState(DMKVNState vn) : IdealizedState {
    //TODO: extend these with orderings for background, music, etc.
    public override async Task ActualizeOnNewState() {
        await base.ActualizeOnNewState();
        await FadeIn();
    }

    public override async Task DeactualizeOnEndState() {
        await FadeOut();
        await base.DeactualizeOnEndState();
        vn.MainDialogue?.Clear();
    }

    protected abstract Task FadeIn();
    protected abstract Task FadeOut();
}
}