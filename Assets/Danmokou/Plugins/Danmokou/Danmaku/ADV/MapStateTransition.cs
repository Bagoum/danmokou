using System;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using BagoumLib.Tasks;

namespace Danmokou.ADV {

/// <summary>
/// Safely manages transitions between data states operated over by <see cref="MapStateManager{I,D}"/>.
/// </summary>
public record MapStateTransition<I, D>(MapStateManager<I, D> MapStates) where I: IdealizedState where D: ADVData {
    public Task? mapUpdateTask { get; private set; }
    //Note: you'll have to do some plumbing to ensure that this enqueue mechanism doesn't get clogged
    // when teleporting using the world map while an update is occuring.
    //Pushing a vn skip operation or two should work.
    private D? nextMapUpdateData;

    public void UpdateMapData(D nextData) {
        nextMapUpdateData = nextData ?? throw new Exception("No data provided");
        Recheck();
    }

    private void Recheck() {
        if (mapUpdateTask is {IsCompleted : true})
            mapUpdateTask = null;
        if (mapUpdateTask == null && nextMapUpdateData != null) {
            mapUpdateTask = MapStates
                .UpdateMaps(nextMapUpdateData, nextMapUpdateData.CurrentMap)
                .ContinueWithSync(Recheck);
        }
    }
}
}