using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Core;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN;
using Suzunoya.ControlFlow;

namespace Danmokou.ADV {

/// <summary>
/// The process executing an ADV instance. This is subclassed for each game.
/// </summary>
public interface IExecutingADV : IRegularUpdater, IDisposable {
    ADVInstance Inst { get; }
    ADVData ADVData => Inst.ADVData;
    ADVManager Manager => Inst.Manager;
    DMKVNState VN => Inst.VN;
    IMapStateManager MapStates { get; }
    Task Run();
}

/// <summary>
/// See <see cref="IExecutingADV"/>
/// </summary>
/// <typeparam name="I">Type of idealized state container</typeparam>
/// <typeparam name="D">Type of save data</typeparam>
public interface IExecutingADV<I, D> : IExecutingADV where I : ADVIdealizedState where D : ADVData {
    new MapStateManager<I, D> MapStates { get; }
    IMapStateManager IExecutingADV.MapStates => MapStates; 
}

/// <summary>
/// Baseline implementation of <see cref="IExecutingADV"/> that can be used for pure VN games with no map control.
/// </summary>
public class BarebonesExecutingADV<D> : IExecutingADV<ADVIdealizedState, D> where D : ADVData {
    public void RegularUpdate() {}

    public void Dispose() { }
    public ADVInstance Inst { get; }
    private readonly Func<Task> executor;
    public MapStateManager<ADVIdealizedState, D> MapStates { get; }
    
    public BarebonesExecutingADV(ADVInstance inst, Func<Task> executor) {
        this.Inst = inst;
        this.executor = executor;
        this.MapStates = new MapStateManager<ADVIdealizedState, D>(() => new(this));
    }

    public Task Run() => executor();
}

/// <summary>
/// Implementation of <see cref="IExecutingADV"/> for ADV games requiring assertion logic and map controls.
/// <br/>Almost all game configuration is done in abstract method <see cref="ConfigureMapStates"/>.
/// </summary>
public abstract class ExecutingADVGame<I, D> : IExecutingADV<I, D> where I : ADVIdealizedState where D : ADVData {
    public ADVInstance Inst { get; }
    public ADVManager Manager => Inst.Manager;
    /// <inheritdoc cref="ADVInstance.VN"/>
    public DMKVNState VN => Inst.VN;
    /// <summary>
    /// Same as <see cref="VN"/> but easier to type
    /// </summary>
    public DMKVNState vn => VN;
    //Note that underlying data may change due to proxy loading
    protected D Data => (Inst.ADVData as D) ?? throw new Exception("Instance data is of wrong type");
    /// <summary>
    /// Event called immediately after save data is changed, and before assertions are recomputed.
    /// </summary>
    public Evented<D> DataChanged { get; }
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
    private readonly List<IDisposable> mapLocalTokens = new();
    /// <summary>
    /// Maps a BCtx ID to the corresponding BCtx for all BCtxes in the game
    /// </summary>
    protected readonly Dictionary<string, BoundedContext<Unit>> bctxes = new();
    private readonly UITKRerenderer rerenderer;
    
    protected readonly Evented<(string? prevMap, string newMap)> MapWillUpdate;

    /// <summary>
    /// Set a disposable to be automatically disposed when the map changes.
    /// </summary>
    protected T DisposeWithMap<T>(T token) where T: IDisposable {
        mapLocalTokens.Add(token);
        return token;
    }
    
    /// <summary>
    /// Change the current map. Always call this method instead of setting <see cref="ADVData.CurrentMap"/>,
    ///  as it triggers <see cref="MapWillUpdate"/>.
    /// </summary>
    /// <param name="map">New map to go to</param>
    /// <param name="updater">Optional data update step that will run before the map change</param>
    /// <returns></returns>
    protected Task GoToMap(string map, Action<D>? updater = null) {
        var prev = Data.CurrentMap;
        if (prev != map) {
            return UpdateData(adv => {
                updater?.Invoke(adv);
                adv.CurrentMap = map;
                MapWillUpdate.OnNext((prev, map));
            });
        }
        return Task.CompletedTask;
    }
    /// <inheritdoc cref="GoToMap"/>
    protected UIResult GoToMapUI(string map, Action<D>? updater = null) {
        var prev = Data.CurrentMap;
        if (prev != map) {
            GoToMap(map, updater);
            return new UIResult.StayOnNode();
        }
        return new UIResult.StayOnNode(true);
    }

    public ExecutingADVGame(ADVInstance inst) {
        this.Inst = inst;
        prevMap = Data.CurrentMap;
        MapWillUpdate = new((null, prevMap));
        //probably don't need to add these to tokens as they'll be destroyed with VN destruction.
        rerenderer = VN.Add(new UITKRerenderer(UIBuilderRenderer.ADV_INTERACTABLES_GROUP), sortingID: 10000);
        MapStates = ConfigureMapStates();
        mapTransition = new(MapStates);
        tokens.Add(mapTransition.ExecutingTransition.Subscribe(b => {
            if (b)
                transitionToken.Add(Manager.ADVState.AddConst(ADVManager.State.Waiting));
            else
                transitionToken.DisposeAll();
        }));
        tokens.Add(MapStates.MapEndStateDeactualized.Subscribe(_ => mapLocalTokens.DisposeAll()));
        tokens.Add(VN.InstanceDataChanged.Subscribe(_ => UpdateDataV(_ => { })));
        tokens.Add(ETime.RegisterRegularUpdater(this));
        DataChanged = new Evented<D>(Data);
    }

    public abstract void RegularUpdate();

    protected void UpdateDataV(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        _ = UpdateData(updater, transition).ContinueWithSync();
    }
    protected Task UpdateData(Action<D> updater, MapStateTransitionSettings<I>? transition = null) {
        updater(Data);
        DataChanged.OnNext(Data);
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
    public async Task Run() {
        //Data.ExecNum = GameManagement.ExecutionNumber;
        await mapTransition.UpdateMapData(Data, new MapStateTransitionSettings<I> {
            ExtraAssertions = (map, s) => {
                if (map == prevMap && Inst.Request.LoadProxyData?.VNData is { Location: { } l} replayer) {
                    //If saved during a VN segment, load into it
                    Inst.VN.LoadToLocation(l, replayer, () => {
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

    /// <summary>
    /// Map setup function run once during game initialization.
    /// <br/>Subclasses must override this to make data-dependent assertions on maps.
    /// <br/>Assertions will be re-evaluated whenever the instance data changes.
    /// <br/>Example usage, where SomeEntity appears on MyMapName after QuestYYY is accepted:
    /// <code>
    /// ms.ConfigureMap("MyMapName", (i, d) => {
    ///   if (d.QuestState.QuestYYY >= QuestState.ACCEPTED) {
    ///     i.Assert(new EntityAssertion&lt;SomeEntity&gt;(vn));
    /// ...
    /// </code>
    /// </summary>
    protected abstract MapStateManager<I, D> ConfigureMapStates();
    
    public void Dispose() {
        tokens.DisposeAll();
        transitionToken.DisposeAll();
        mapLocalTokens.DisposeAll();
    }
}

}