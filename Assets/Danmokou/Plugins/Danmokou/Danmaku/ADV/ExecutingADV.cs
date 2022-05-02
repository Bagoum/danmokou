using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.UI;
using Danmokou.VN;
using Suzunoya.ControlFlow;

namespace Danmokou.ADV {

public interface IExecutingADV : IRegularUpdater, IDisposable {
    ADVInstance Inst { get; }
    ADVData ADVData => Inst.ADVData;
    ADVManager Manager => Inst.Manager;
    DMKVNState vn => Inst.VN;
    IMapStateManager MapStates { get; }
    Task Run();
}

public interface IExecutingADV<I, D> : IExecutingADV where I : ADVIdealizedState where D : ADVData {
    new MapStateManager<I, D> MapStates { get; }
    IMapStateManager IExecutingADV.MapStates => MapStates; 
}

public class BarebonesExecutingADV<D> : IExecutingADV<ADVIdealizedState, D> where D : ADVData {
    public void RegularUpdate() {}

    public void Dispose() {
        throw new NotImplementedException();
    }
    public ADVInstance Inst { get; }
    private readonly Func<Task> executor;
    public MapStateManager<ADVIdealizedState, D> MapStates { get; }
    
    public BarebonesExecutingADV(ADVInstance inst, Func<Task> executor) {
        this.Inst = inst;
        this.executor = executor;
        this.MapStates = new MapStateManager<ADVIdealizedState, D>(() => new(Inst));
    }

    public Task Run() => executor();
}

public abstract class ExecutingADVGame<I, D> : IExecutingADV<I, D> where I : ADVIdealizedState where D : ADVData {
    public ADVInstance Inst { get; }
    public ADVManager Manager => Inst.Manager;
    public DMKVNState vn => Inst.VN;
    //Note that underlying data may change due to proxy loading
    protected D Data => (Inst.ADVData as D) ?? throw new Exception("Instance data is of wrong type");
    private string prevMap;
    public MapStateManager<I, D> MapStates { get; }
    private readonly MapStateTransition<I, D> mapTransition;
    protected Task MapTransitionTask => mapTransition.MapUpdateTask ?? Task.CompletedTask;
    /// <summary>
    /// True when the current map is changing (eg. from Hakurei Shrine to Moriya Shrine).
    /// </summary>
    private readonly Evented<bool> executingCrossMapTransition = new(false);
    protected readonly TaskCompletionSource<Unit> completion = new();
    protected readonly List<IDisposable> tokens = new();
    private readonly List<IDisposable> transitionToken = new();
    /// <summary>
    /// Maps a BCtx ID to the corresponding BCtx for all BCtxes in the game
    /// </summary>
    protected readonly Dictionary<string, BoundedContext<Unit>> bctxes = new();
    private readonly UITKRerenderer rerenderer;
    
    protected readonly Evented<(string? prevMap, string newMap)> MapWillUpdate;
    
    protected void GoToMap(string map, Action<D>? updater = null) {
        var prev = Data.CurrentMap;
        if (prev != map) {
            UpdateDataV(adv => {
                updater?.Invoke(adv);
                adv.CurrentMap = map;
                MapWillUpdate.OnNext((prev, map));
            });
        }
    }

    public ExecutingADVGame(ADVInstance inst) {
        this.Inst = inst;
        prevMap = Data.CurrentMap;
        MapWillUpdate = new((null, prevMap));
        //probably don't need to add these to tokens as they'll be destroyed with VN destruction.
        rerenderer = vn.Add(new UITKRerenderer(UIBuilderRenderer.ADV_INTERACTABLES_GROUP), sortingID: 10000);
        MapStates = ConfigureMapStates();
        mapTransition = new(MapStates);
        tokens.Add(mapTransition.ExecutingTransition.Subscribe(b => {
            if (b)
                transitionToken.Add(Manager.ADVState.AddConst(ADVManager.State.Waiting));
            else
                transitionToken.DisposeAll();
        }));
        tokens.Add(vn.InstanceDataChanged.Subscribe(_ => UpdateDataV(_ => { })));
        tokens.Add(ETime.RegisterRegularUpdater(this));
    }

    public abstract void RegularUpdate();

    protected void UpdateDataV(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        _ = UpdateData(updater, transition).ContinueWithSync();
    }
    protected Task UpdateData(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        updater(Data);
        return UpdateMap(transition);
    }

    //Function to change the map configuration. Runs whenever the game data changes
    private async Task UpdateMap(MapStateTransitionSettings<I>? transition) {
        if (Data.CurrentMap != prevMap) {
            prevMap = Data.CurrentMap;
            executingCrossMapTransition.OnNext(true);
            //Clear whatever existing update operation is occuring (TODO: test w/w/o this)
            Inst.VN.SkipOperation();
            Inst.VN.Flush();
            //Then push the map update operation
            await mapTransition.UpdateMapData(Data, transition);
            executingCrossMapTransition.OnNext(false);
        } else {
            //Not a CurrentMap transition
            await mapTransition.UpdateMapData(Data, transition);
        }
    }
    
    /// <summary>
    /// Boilerplate code for running an ADV-based game. 
    /// </summary>
    public virtual async Task Run() {
        //Data.ExecNum = GameManagement.ExecutionNumber;
        await mapTransition.UpdateMapData(Data, new MapStateTransitionSettings<I> {
            ExtraAssertions = (map, s) => {
                if (map == prevMap && Inst.Request.LoadProxyData?.VNData.Location is { } l) {
                    //If saved during a VN segment, load into it
                    Inst.VN.LoadToLocation(l, () => {
                        Inst.Request.FinalizeProxyLoad();
                        UpdateDataV(_ => { });
                    });
                    if (!s.SetEntryVN(bctxes[l.Contexts[0]], RunOnEntryVNPriority.LOAD))
                        throw new Exception("Couldn't set load entry VN");
                }
            }
        });
        //This is when the entire game finishes
        await completion.Task;
    }

    protected abstract MapStateManager<I, D> ConfigureMapStates();
    
    public void Dispose() {
        tokens.DisposeAll();
        transitionToken.DisposeAll();
    }
}

}