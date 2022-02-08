using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib.Assertions;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;

namespace Danmokou.ADV {
/// <summary>
/// Manages the idealized and actualized states for many concurrent maps.
/// <br/>At most one map may be actualized at a time (see <see cref="CurrentMap"/>).
/// </summary>
/// <typeparam name="I">Type of idealized state</typeparam>
/// <typeparam name="D">Type of game data</typeparam>
public record MapStateManager<I, D>(Func<I> Constructor) : IDisposable where I : IdealizedState {
    private Maybe<D> LastData = Maybe<D>.None;
    private readonly Dictionary<string, (I state, Action<I, D> stateConstructor)> mapStates = new();
    private readonly List<IDisposable> tokens = new();
    public string CurrentMap { get; private set; } = "";
    
    /// <summary>
    /// Configure a map definition. Note that a map definition must be configured
    ///  before it is set current via MapChanged.
    /// </summary>
    /// <param name="mapKey">Key to associate with the map</param>
    /// <param name="stateConstructor">Process that creates assertions for the map depending on game data</param>
    /// <exception cref="Exception">Thrown if the key is already configured.</exception>
    public void ConfigureMap(string mapKey, Action<I, D> stateConstructor) {
        if (mapStates.ContainsKey(mapKey))
            throw new Exception($"Map constructor already defined for {mapKey}");

        var state = Constructor();
        if (LastData.Try(out var d))
            stateConstructor(state, d);
        mapStates[mapKey] = (state, stateConstructor);
    }

    /// <summary>
    /// Update all map definitions with a new game data object.
    /// </summary>
    public async Task UpdateMaps(D data, string newCurrentMap) {
        LastData = data;
        if (newCurrentMap != CurrentMap && mapStates.TryGetValue(CurrentMap, out var s))
            await s.state.DeactualizeOnEndState();
        CurrentMap = newCurrentMap;
        foreach (var (k, v) in mapStates.ToArray()) {
            var ns = Constructor();
            v.stateConstructor(ns, data);
            if (k == CurrentMap)
                await ns.Actualize(v.state);
            mapStates[k] = (ns, v.stateConstructor);
        }
    }

    public void Dispose() {
        foreach (var t in tokens)
            t.Dispose();
        tokens.Clear();
    }
}
}