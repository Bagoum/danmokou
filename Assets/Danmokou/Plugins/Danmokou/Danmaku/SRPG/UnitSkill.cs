using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using Danmokou.SRPG.Diffs;
using Danmokou.SRPG.Nodes;
using UnityEngine;

namespace Danmokou.SRPG {

/// <summary>
/// The broad category of this skill.
/// </summary>
public enum UnitSkillType {
    Attack
}

/// <summary>
/// Abstract descriptions of targeted actions that a unit can take after moving.
/// </summary>
public interface IUnitSkill {
    LString Name { get; }
    int MinRange { get; }
    int MaxRange { get; }
    /// <inheritdoc cref="UnitSkillShape"/>
    UnitSkillShape Shape { get; }
    /// <inheritdoc cref="UnitSkillType"/>
    UnitSkillType Type { get; }

    /// <summary>
    /// Apply this skill as part of the execution of a <see cref="UnitSkill"/>.
    /// <br/>In most cases, this function should do nothing internally and only return
    ///  resultant diffs (such as ReduceUnitHP).
    /// </summary>
    /// <returns>A list of diffs that are caused by this skill,
    /// and should be also applied to the game state.
    /// <see cref="IGameDiff.CausedBy"/> will be overwritten by the consumer.</returns>
    List<IGameDiff> Apply(GameState gs, UnitSkill skill);
    
    void Unapply(GameState gs, UnitSkill skill);
}

/// <summary>
/// The shape of the skill.
/// </summary>
public record UnitSkillShape(params (Vector2Int offset, float multiplier)[] Points) {
    public bool Rotatable { get; init; } = true;

    public static UnitSkillShape Single { get; } = new((new(0, 0), 1)) { Rotatable = false };
}

public record BasicAttackSkill(LString Name, int MinRange, int MaxRange, int Power) : IUnitSkill {
    public UnitSkillShape Shape => UnitSkillShape.Single;
    public UnitSkillType Type => UnitSkillType.Attack;
    
    public List<IGameDiff> Apply(GameState gs, UnitSkill skill) {
        var caused = ListCache<IGameDiff>.Get();
        var baseDmg = skill.Unit.Stats.Atk + Power;
        foreach (var (offset, mult) in Shape.Points) {
            var node = skill.Target;
            //TODO: get node after offset
            if (node.Unit is {} u)
                caused.Add(new ReduceUnitHP(u, Math.Max(1, (int)MathF.Ceiling(mult * (baseDmg - u.Stats.Def)))));
        }
        return caused;
    }

    public void Unapply(GameState gs, UnitSkill skill) {
        
    }
}

}