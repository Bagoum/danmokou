using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.SRPG.Nodes;
using Newtonsoft.Json;
using UnityEngine;

namespace Danmokou.SRPG {

[Flags]
public enum MovementFlags {
    Default = 0,
    Flying = 1 << 1,
}

public enum UnitStatus {
    /// <summary>
    /// The unit cannot act because the current turn belongs to a different faction.
    /// </summary>
    NotMyTurn,
    /// <summary>
    /// The unit cannot act because its turn has been cancelled or it has already acted on this faction turn.
    /// </summary>
    Exhausted,
    /// <summary>
    /// The unit can move and act.
    /// </summary>
    CanMove,
    /// <summary>
    /// The unit has moved and now must act.
    /// </summary>
    MustAct,
    /// <summary>
    /// The unit is dead.
    /// </summary>
    Graveyard
}

public class Unit {
    public Faction Team { get; }
    [JsonIgnore] public GameState State => Team.State;
    public string Key { get; }
    public StatBlock Stats { get; }
    public LString Name { get; init; }
    public Node? Location { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.NotMyTurn;
    public MovementFlags Movement { get; init; }
    public UnitSkill[] Skills { get; }
    public Vector4 Theme1 { get; init; } = ColorScheme.GetColor("red");
    public Vector4 Theme2 { get; init; } = ColorScheme.GetColor("purple");
    [JsonIgnore] public IEnumerable<IUnitSkill> AttackSkills => Skills.Where(s => s.Type is UnitSkillType.Attack);
    
    public Unit(Faction team, string key, StatBlock stats, params UnitSkill[] skills) {
        this.Team = team;
        this.Key = key;
        this.Stats = stats;
        this.Skills = skills;
        this.Name = key;
    }

    public void AssertIsAt(Node target) {
        if (Location != target || target?.Unit != this)
            throw new Exception($"Unit {this} is not at location {target}");
    }
    public void UpdateStatus(UnitStatus? req, UnitStatus nxt) {
        if (req != null && Status != req)
            throw new Exception($"Unit {this} must have status {req}, but is currently {Status}");
        Status = nxt;
    }

    public void SetLocationTo(Node? target) {
        if (Location != null)
            Location.Unit = null;
        Location = target;
        if (Location != null)
            Location.Unit = this;
        State.ActiveRealizer?.SetUnitLocation(this, target);
    }

    public override string ToString() => $"{Name} ({Team})";
}
}