using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.Core;

namespace Danmokou.ADV {
public record MapStateTransitionSettings<I> {
    public Action<string, I>? ExtraAssertions { get; init; }
    public bool SimultaneousActualization { get; init; } = false;
}

public interface IMapStateManager {
    ADVIdealizedState CurrentMapState { get; }
}

/// <summary>
/// A callback provided to consumers that calls Assert on the map with the associated key.
/// </summary>
public delegate void GenericAssert(string map, params IAssertion[] assertions);

/// <summary>
/// Manages the idealized and actualized states for many concurrent maps.
/// <br/>At most one map may be actualized at a time (see <see cref="CurrentMap"/>).
/// </summary>
/// <typeparam name="I">Type of idealized state</typeparam>
/// <typeparam name="D">Type of game data</typeparam>
public record MapStateManager<I, D>(Func<I> Constructor) : IMapStateManager, IDisposable where I : ADVIdealizedState {
    private class MapConfig {
        public Action<I, D> StateConstructor { get; }
        public I State { get; set; }
        
        public MapConfig(Action<I, D> stateConstructor, I state) {
            StateConstructor = stateConstructor;
            State = state;
        }
    }

    private readonly Dictionary<string, MapConfig> mapStates = new();
    private readonly List<Action<GenericAssert, D>> genericAsserts = new();
    private readonly List<IDisposable> tokens = new();
    public string CurrentMap { get; private set; } = "";
    public I CurrentMapState => mapStates[CurrentMap].State;
    ADVIdealizedState IMapStateManager.CurrentMapState => CurrentMapState;
    
    
    /// <summary>
    /// Configure a map definition. This should be done for all maps before the game code is run.
    /// </summary>
    /// <param name="mapKey">Key to associate with the map</param>
    /// <param name="stateConstructor">Process that creates assertions for the map depending on game data</param>
    /// <exception cref="Exception">Thrown if the key is already configured.</exception>
    public void ConfigureMap(string mapKey, Action<I, D> stateConstructor) {
        if (mapStates.ContainsKey(mapKey))
            throw new Exception($"Map constructor already defined for {mapKey}");
        mapStates[mapKey] = new(stateConstructor, Constructor());
    }

    /// <summary>
    /// Configure map definitions for all maps. This should be done before the game code is run.
    /// </summary>
    /// <param name="stateConstructor">Process that creates assertions for all maps depending on game data</param>
    public void ConfigureGeneric(Action<GenericAssert, D> stateConstructor) {
        genericAsserts.Add(stateConstructor);
    }

    /// <summary>
    /// Update all map definitions with a new game data object.
    /// </summary>
    public async Task UpdateMaps(D data, string newCurrentMap, MapStateTransitionSettings<I>? settings = null) {
        Logs.Log($"Updating map state for next map {newCurrentMap} (current map is {CurrentMap})...");
        if (newCurrentMap != CurrentMap && mapStates.TryGetValue(CurrentMap, out var s)) {
            Logs.Log($"As the map has changed, the current map {CurrentMap} will be end-state deactualized.");
            await s.State.DeactualizeOnEndState();
        }
        var generics = new Dictionary<string, List<IAssertion>>();
        void GenericAsserter(string map, params IAssertion[] assertions) {
            if (!generics.TryGetValue(map, out var l))
                l = generics[map] = new();
            l.AddRange(assertions);
        }
        foreach (var g in genericAsserts)
            g(GenericAsserter, data);
        
        CurrentMap = newCurrentMap;
        var prevStateForCurrMap = CurrentMapState;
        foreach (var (map, cfg) in mapStates) {
            //Create a new idealized state and apply assertions to it
            var ns = Constructor();
            cfg.StateConstructor(ns, data);
            if (generics.TryGetValue(map, out var lis))
                ns.Assert(lis);
            settings?.ExtraAssertions?.Invoke(map, ns);
            cfg.State = ns;
        }
        //Actualize only the current map based on its previous state
        await mapStates[CurrentMap].State.Actualize(prevStateForCurrMap, settings?.SimultaneousActualization ?? false);

        Logs.Log($"Finished updating map state for next map {newCurrentMap}.");
    }

    public void Dispose() {
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
    }
}
}