using System;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Events;
using BagoumLib.Tasks;

namespace Danmokou.ADV {

/// <summary>
/// Safely manages transitions between data states operated over by <see cref="MapStateManager{I,D}"/>.
/// </summary>
public class MapStateTransition<I, D> where I: ADVIdealizedState where D: ADVData {
    private class LazyTask {
        public readonly Func<Task> gen;
        private Task? generated;
        public Task Task => generated ??= gen();

        public LazyTask(Func<Task> gen) {
            this.gen = gen;
        }
    }
    public MapStateManager<I, D> MapStates { get; }
    /// <summary>
    /// True when the map state is changing.
    /// <br/>Consumers may want to disable certain functionalities while this is true.
    /// </summary>
    public Evented<bool> ExecutingTransition { get; } = new(false);
    private LazyTask? _mapUpdate;
    public Task? MapUpdateTask => _mapUpdate?.Task;
    //Note: you'll have to do some plumbing to ensure that this enqueue mechanism doesn't get clogged
    // when teleporting using the world map while an update is occuring.
    //Pushing a vn skip operation or two should work.
    private (D data, MapStateTransitionSettings<I>? settings, TaskCompletionSource<Unit> finish)? nextMapUpdateData;

    public MapStateTransition(MapStateManager<I, D> mapStates) {
        this.MapStates = mapStates;
    }

    public Task UpdateMapData(D nextData, MapStateTransitionSettings<I>? settings) {
        var tcs = new TaskCompletionSource<Unit>();
        nextMapUpdateData = (nextData ?? throw new Exception("No data provided"), settings, tcs);
        if (_mapUpdate == null)
            TaskIsDone();
        return tcs.Task;
    }

    private void TaskIsDone() {
        _mapUpdate = null;
        if (nextMapUpdateData.Try(out var nd)) {
            nextMapUpdateData = null;
            ExecutingTransition.PublishIfNotSame(true);
            _mapUpdate = new(() => 
                MapStates.UpdateMaps(nd.data, nd.data.CurrentMap, nd.settings).ContinueWithSync(() => {
                TaskIsDone();
                nd.finish.SetResult(default);
            }));
            _ = _mapUpdate.Task;
        } else 
            ExecutingTransition.PublishIfNotSame(false);
    }
}
}