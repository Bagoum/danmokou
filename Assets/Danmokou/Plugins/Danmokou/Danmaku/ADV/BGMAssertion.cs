using System.Threading.Tasks;
using BagoumLib.Assertions;
using Danmokou.VN;

namespace Danmokou.ADV {
public record BGMAssertion(DMKVNState vn, string key) : IAssertion<BGMAssertion> {
    private DMKVNState.RunningAudioTrackProxy track = null!;
    public string? ID => key;

    public Task ActualizeOnNewState() {
        track = vn.RunBGM(key);
        return Task.CompletedTask;
    }

    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();

    public Task DeactualizeOnEndState() {
        track.FadeOutDestroy(1f);
        return Task.CompletedTask;
    }

    public Task DeactualizeOnNoSucceeding() => DeactualizeOnEndState();

    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);

    public Task _Inherit(BGMAssertion prev) {
        this.track = prev.track;
        return Task.CompletedTask;
    }
}
}