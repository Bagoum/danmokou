using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.SRPG.Nodes;
using UnityEngine;

namespace Danmokou.SRPG.Diffs {
/// <summary>
/// Modifications that can be sequentially added to the game state, including
///  player actions/AI actions (eg. moving a unit) and caused actions (eg. passive effects).
/// </summary>
public interface IGameDiff {
    /// <summary>
    /// Apply this action to the game state.
    /// </summary>
    /// <returns>Optionally, a list of diffs that are caused by this diffs,
    /// and should be also applied to the game state.
    /// <see cref="CausedBy"/> will be overwritten by the consumer.</returns>
    List<IGameDiff>? Apply(GameState gs);
    
    /// <summary>
    /// Unapply this action (that was already applied) to the game state.
    /// <br/>Does not need to handle any caused actions returned by <see cref="Apply"/>.
    /// </summary>
    void Unapply(GameState gs);
    
    /// <summary>
    /// The action that caused this one.
    /// </summary>
    IGameDiff? CausedBy { get; set; }
}


public record NewUnit(Node At, Unit Unit) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    List<IGameDiff>? IGameDiff.Apply(GameState gs) {
        if (At.Unit != null)
            throw new Exception($"Cannot instantiate new unit at occupied node {At}");
        if (Unit.Location != null)
            throw new Exception($"Unit {Unit} cannot be re-instantiated");
        gs.Units.Add(Unit);
        gs.ActiveRealizer?.Instantiate(this);
        Unit.SetLocationTo(At);
        return null;
    }

    void IGameDiff.Unapply(GameState gs) {
        Unit.SetLocationTo(null);
        gs.Units.Remove(Unit);
    }
}

public record MoveUnit(Node From, Node To, Unit Unit, List<Node>? Path) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    List<IGameDiff>? IGameDiff.Apply(GameState gs) {
        Unit.AssertIsAt(From);
        Unit.UpdateStatus(UnitStatus.CanMove, UnitStatus.MustAct);
        Unit.SetLocationTo(To);
        return null;
    }

    void IGameDiff.Unapply(GameState gs) {
        Unit.AssertIsAt(To);
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.CanMove);
        Unit.SetLocationTo(From);
    }

    public override string ToString() => $"MoveUnit {{ {Unit}: {From} -> {To} }}";
}


public record ReduceUnitHP(Unit Unit, int Damage) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    
    public List<IGameDiff>? Apply(GameState gs) {
        //TODO handle unit death
        Unit.Stats.CurrHP -= Damage;
        return null;
    }

    public void Unapply(GameState gs) {
        Unit.Stats.CurrHP += Damage;
    }
}

/// <summary>
/// Interface for actions a unit can take after moving.
/// </summary>
public interface IUnitSkillDiff : IGameDiff {
    Unit Unit { get; }
}

/// <summary>
/// The "Wait" action, where a unit exhausts itself without attacking/healing/etc.
/// </summary>
public record UnitWait(Unit Unit) : IUnitSkillDiff {
    public IGameDiff? CausedBy { get; set; }
    public List<IGameDiff>? Apply(GameState gs) {
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.Exhausted);
        return null;
    }

    public void Unapply(GameState gs) {
        Unit.UpdateStatus(UnitStatus.Exhausted, UnitStatus.MustAct);
    }
}

/// <summary>
/// The usage of a defined <see cref="IUnitSkill"/>, such as an attack or heal command.
/// </summary>
public record UnitSkill(Unit Unit, Node Target, IUnitSkill Skill) : IUnitSkillDiff {
    public IGameDiff? CausedBy { get; set; }
    
    public List<IGameDiff> Apply(GameState gs) {
        var caused = Skill.Apply(gs, this);
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.Exhausted);
        return caused;
    }

    public void Unapply(GameState gs) {
        Unit.UpdateStatus(UnitStatus.Exhausted, UnitStatus.MustAct);
        Skill.Unapply(gs, this);
    }
}


}