using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.SRPG.Diffs;
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
    /// Undo <see cref="Instantiate"/>.
    /// </summary>
    void Uninstantiate(NewUnit nu);

    /// <summary>
    /// Disable a unit (but do not destroy it).
    /// </summary>
    void Disable(GraveyardUnit gu);

    /// <summary>
    /// Undo <see cref="Disable"/>.
    /// </summary>
    void Undisable(GraveyardUnit gu);

    /// <summary>
    /// Position a unit in the world (no animation; use <see cref="Animate(MoveUnit,ICancellee)"/> for animation).
    /// </summary>
    void SetUnitLocation(Unit u, Node? to);
    
    /// <summary>
    /// Animate a provided <see cref="IGameDiff"/>.
    /// </summary>
    Task? Animate(IGameDiff diff, ICancellee cT, SubList<IGameDiff> caused) => diff switch {
        MoveUnit mu => Animate(mu, cT),
        GraveyardUnit gu => Animate(gu, cT),
        UseUnitSkill skill => Animate(skill, cT, caused),
        ReduceUnitHP ruhp => Animate(ruhp, cT),
        GameState.StartGame sg => Animate(sg, cT),
        GameState.SwitchFactionTurn st => Animate(st, cT),
        _ => null
    };

    Task? Animate(MoveUnit ev, ICancellee cT);
    Task? Animate(GraveyardUnit ev, ICancellee cT);
    Task? Animate(UseUnitSkill ev, ICancellee cT, SubList<IGameDiff> caused);
    Task? Animate(ReduceUnitHP ev, ICancellee cT);
    Task? Animate(GameState.StartGame ev, ICancellee cT);
    Task? Animate(GameState.SwitchFactionTurn ev, ICancellee cT);
}



}