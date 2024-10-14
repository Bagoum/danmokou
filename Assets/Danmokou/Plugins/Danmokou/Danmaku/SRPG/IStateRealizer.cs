using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.SRPG.Actions;
using Danmokou.SRPG.Nodes;
using UnityEngine;

namespace Danmokou.SRPG {
/// <summary>
/// Interface for converting pure actions into world changes.
/// <br/>Not called in simulation mode.
/// </summary>
public interface IStateRealizer {
    /// <summary>
    /// Create a new unit in the world.
    /// </summary>
    void Instantiate(NewUnit nu);

    /// <summary>
    /// Position a unit in the world (no animation; use <see cref="Animate(MoveUnit,ICancellee)"/> for animation).
    /// </summary>
    void SetUnitLocation(Unit u, Node? from, Node? to);
    
    /// <summary>
    /// Animate a provided <see cref="IUnitAction"/>.
    /// </summary>
    Task? Animate(IUnitAction action, ICancellee cT) => action switch {
        MoveUnit mu => Animate(mu, cT),
        GameState.StartGame sg => Animate(sg, cT),
        GameState.SwitchFactionTurn st => Animate(st, cT),
        _ => null
    };

    Task? Animate(MoveUnit ev, ICancellee cT);
    Task? Animate(GameState.StartGame ev, ICancellee cT);
    Task? Animate(GameState.SwitchFactionTurn ev, ICancellee cT);
}



}