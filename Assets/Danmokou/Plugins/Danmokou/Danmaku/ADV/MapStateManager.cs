using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using Danmokou.Core;

namespace Danmokou.ADV {
/// <summary>
/// Manages the idealized and actualized states for many concurrent maps.
/// <br/>At most one map may be actualized at a time (see <see cref="CurrentMap"/>).
/// </summary>
/// <typeparam name="I">Type of idealized state</typeparam>
/// <typeparam name="D">Type of game data</typeparam>
public record MapStateManager<I, D>(Func<I> Constructor) : IDisposable where I : IdealizedState {
    private readonly Dictionary<string, (I state, Action<I, D> stateConstructor)> mapStates = new();
    private readonly List<IDisposable> tokens = new();
    public string CurrentMap { get; private set; } = "";
    
    /// <summary>
    /// Configure a map definition. This should be done for all maps before the game code is run.
    /// </summary>
    /// <param name="mapKey">Key to associate with the map</param>
    /// <param name="stateConstructor">Process that creates assertions for the map depending on game data</param>
    /// <exception cref="Exception">Thrown if the key is already configured.</exception>
    public void ConfigureMap(string mapKey, Action<I, D> stateConstructor) {
        if (mapStates.ContainsKey(mapKey))
            throw new Exception($"Map constructor already defined for {mapKey}");

        var state = Constructor();
        mapStates[mapKey] = (state, stateConstructor);
    }

    /// <summary>
    /// Update all map definitions with a new game data object.
    /// </summary>
    public async Task UpdateMaps(D data, string newCurrentMap, Action<string, I>? extraAssertions = null) {
        Logs.Log($"Updating map state for next map {newCurrentMap} (current map is {CurrentMap})...");
        if (newCurrentMap != CurrentMap && mapStates.TryGetValue(CurrentMap, out var s)) {
            Logs.Log($"As the map has changed, the current map {CurrentMap} will be end-state deactualized.");
            await s.state.DeactualizeOnEndState();
        }
        CurrentMap = newCurrentMap;
        foreach (var (map, v) in mapStates.ToArray()) {
            //Create a new idealized state and apply assertions to it
            var ns = Constructor();
            v.stateConstructor(ns, data);
            extraAssertions?.Invoke(map, ns);
            //If the new idealized state is the actualized one, then actualize it based on the previous state
            if (map == CurrentMap)
                await ns.Actualize(v.state);
            mapStates[map] = (ns, v.stateConstructor);
        }
        Logs.Log($"Finished updating map state for next map {newCurrentMap}.");
    }

    public void Dispose() {
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
    }
}
}