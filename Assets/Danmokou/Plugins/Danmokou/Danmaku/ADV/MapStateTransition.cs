using System;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Events;
using BagoumLib.Tasks;

namespace Danmokou.ADV {

/// <summary>
/// Safely manages transitions between data states operated over by <see cref="MapStateManager{I,D}"/>.
/// </summary>
public class MapStateTransition<I, D> where I: IdealizedState where D: ADVData {
    public MapStateManager<I, D> MapStates { get; }
    /// <summary>
    /// True when the map state is changing.
    /// <br/>Consumers may want to disable certain functionalities while this is true.
    /// </summary>
    public Evented<bool> ExecutingTransition { get; } = new(false);
    public Task? mapUpdateTask { get; private set; }
    //Note: you'll have to do some plumbing to ensure that this enqueue mechanism doesn't get clogged
    // when teleporting using the world map while an update is occuring.
    //Pushing a vn skip operation or two should work.
    private D? nextMapUpdateData;

    public MapStateTransition(MapStateManager<I, D> mapStates) {
        this.MapStates = mapStates;
    }

    public void UpdateMapData(D nextData) {
        nextMapUpdateData = nextData ?? throw new Exception("No data provided");
        if (mapUpdateTask == null)
            TaskIsDone();
    }

    private void TaskIsDone() {
        mapUpdateTask = null;
        if (nextMapUpdateData != null) {
            var nd = nextMapUpdateData;
            nextMapUpdateData = null;
            ExecutingTransition.PublishIfNotSame(true);
            mapUpdateTask = MapStates.UpdateMaps(nd, nd.CurrentMap);
            //Can't merge this into previous line since continuation can occur before assignment (lmao)
            mapUpdateTask.ContinueWithSync(TaskIsDone);
        } else 
            ExecutingTransition.PublishIfNotSame(false);
    }
}
}