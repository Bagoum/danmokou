using System;
using System.Threading.Tasks;
using BagoumLib.Assertions;

namespace Danmokou.ADV {
/// <summary>
/// An assertion that runs a task on entry (new state/no preceding only). Throws on inherit.
/// </summary>
public record RunOnEntryAssertion(Func<Task> OnEntry) : IAssertion<RunOnEntryAssertion> {
    public (int Phase, int Ordering) Priority { get; set; } = (0, 0);
    public Task ActualizeOnNewState() => OnEntry();

    public Task ActualizeOnNoPreceding() => ActualizeOnNewState();

    public Task DeactualizeOnEndState() => Task.CompletedTask;
    public Task DeactualizeOnNoSucceeding() => Task.CompletedTask;

    public Task Inherit(IAssertion prev) => AssertionHelpers.Inherit(prev, this);
    public Task _Inherit(RunOnEntryAssertion prev) => Task.CompletedTask;
}
}