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
    /// Called before <see cref="Apply"/>. Should not modify the game state.
    /// <br/>If this diffs causes other diffs in turn that can be
    /// known before <see cref="Apply"/>, add those into `caused`. 
    /// (<see cref="CausedBy"/> will be overwritten by the consumer.)
    /// </summary>
    void PreApply(GameState gs, List<IGameDiff> caused) { }
    
    /// <summary>
    /// Apply this diffs to the game state.
    /// <br/>If this diffs causes other diffs in turn, add those into `caused`.
    /// (<see cref="CausedBy"/> will be overwritten by the consumer.)
    ///  Caused diffs should be returned in <see cref="PreApply"/> instead if possible,
    ///  as that makes them inspectable by animation.
    /// </summary>
    void Apply(GameState gs, List<IGameDiff> caused);
    
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

/// <summary>
/// Informational interface for any diff that involves one unit directly interacting with another unit
/// (eg. buffing, attacking, etc).
/// </summary>
public interface IUnitXUnitGameDiff: IGameDiff {
    Unit Target { get; }
}


public record NewUnit(Node At, Unit Unit) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        if (At.Unit != null)
            throw new Exception($"Cannot instantiate new unit at occupied node {At}");
        if (Unit.Location != null)
            throw new Exception($"Unit {Unit} cannot be re-instantiated");
        gs.Units.Add(Unit);
        gs.ActiveRealizer?.Instantiate(this);
        Unit.SetLocationTo(At);
    }

    void IGameDiff.Unapply(GameState gs) {
        Unit.SetLocationTo(null);
        gs.ActiveRealizer?.Uninstantiate(this);
        gs.Units.Remove(Unit);
    }
}

public record GraveyardUnit(Unit Unit) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    private Node? FromLoc { get; set; }
    private UnitStatus FromStatus { get; set; }
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        FromLoc = Unit.Location;
        FromStatus = Unit.Status;
        Unit.SetLocationTo(null);
        gs.ActiveRealizer?.Disable(this);
        Unit.UpdateStatus(null, UnitStatus.Graveyard);
        gs.Units.Remove(Unit);
        gs.CheckForTurnEnd(this, caused);
    }

    void IGameDiff.Unapply(GameState gs) {
        gs.Units.Add(Unit);
        Unit.UpdateStatus(UnitStatus.Graveyard, FromStatus);
        gs.ActiveRealizer?.Undisable(this);
        Unit.SetLocationTo(FromLoc);
    }
    
}

public record MoveUnit(Node From, Node To, Unit Unit, List<Node>? Path) : IGameDiff {
    public IGameDiff? CausedBy { get; set; }
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        Unit.AssertIsAt(From);
        Unit.UpdateStatus(UnitStatus.CanMove, UnitStatus.MustAct);
        Unit.SetLocationTo(To);
    }

    void IGameDiff.Unapply(GameState gs) {
        Unit.AssertIsAt(To);
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.CanMove);
        Unit.SetLocationTo(From);
    }

    public override string ToString() => $"MoveUnit {{ {Unit}: {From} -> {To} }}";
}


public record ReduceUnitHP(Unit Target, int Damage) : IUnitXUnitGameDiff {
    public IGameDiff? CausedBy { get; set; }
    
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        if ((Target.Stats.CurrHP -= Damage) <= 0)
            caused.Add(new GraveyardUnit(Target));
    }

    public void Unapply(GameState gs) {
        Target.Stats.CurrHP += Damage;
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
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.Exhausted);
        gs.CheckForTurnEnd(this, caused);
    }

    public void Unapply(GameState gs) {
        Unit.UpdateStatus(UnitStatus.Exhausted, UnitStatus.MustAct);
    }
}

/// <summary>
/// The usage of a defined <see cref="IUnitSkill"/>, such as an attack or heal command.
/// </summary>
public record UseUnitSkill(Unit Unit, Node Target, IUnitSkill Skill, int Rotation) : IUnitSkillDiff {
    public IGameDiff? CausedBy { get; set; }
    
    void IGameDiff.PreApply(GameState gs, List<IGameDiff> caused) {
        Skill.PreApply(gs, this, caused);
    }
    
    void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
        Skill.Apply(gs, this, caused);
        Unit.UpdateStatus(UnitStatus.MustAct, UnitStatus.Exhausted);
        gs.CheckForTurnEnd(this, caused);
    }

    public void Unapply(GameState gs) {
        Unit.UpdateStatus(UnitStatus.Exhausted, UnitStatus.MustAct);
        Skill.Unapply(gs, this);
    }
}


}