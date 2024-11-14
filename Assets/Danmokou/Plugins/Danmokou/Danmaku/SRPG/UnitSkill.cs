using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.SRPG.Diffs;
using Danmokou.SRPG.Nodes;
using Newtonsoft.Json;
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
    LString Description { get; }
    int MinRange { get; }
    int MaxRange { get; }
    /// <inheritdoc cref="UnitSkillShape"/>
    UnitSkillShape Shape { get; }
    /// <inheritdoc cref="UnitSkillType"/>
    UnitSkillType Type { get; }

    /// <summary>
    /// Called before <see cref="Apply"/>. Should not modify the game state.
    /// <br/>If this skill causes diffs to be produced, add those into `caused`.
    /// (<see cref="IGameDiff.CausedBy"/> will be overwritten by the consumer.)
    /// </summary>
    void PreApply(GameState gs, UseUnitSkill diff, List<IGameDiff> caused);
    
    /// <summary>
    /// Apply this skill as part of the execution of a <see cref="UseUnitSkill"/>.
    /// <br/>If this skill causes diffs to be produced, add those into `caused`.
    /// (<see cref="IGameDiff.CausedBy"/> will be overwritten by the consumer.)
    /// <br/>Caused diffs should be returned in <see cref="PreApply"/> instead of possible.
    /// <br/>In most cases, this function should do nothing internally and only create
    ///  resultant diffs (such as ReduceUnitHP).
    /// </summary>
    void Apply(GameState gs, UseUnitSkill diff, List<IGameDiff> caused);
    
    void Unapply(GameState gs, UseUnitSkill diff);

    bool Reachable(Unit caster, Node target) {
        var dist = (target.Index - caster.Location!.Index).Abs().Sum();
        return (dist >= MinRange && dist <= MaxRange);
    }

    void CreateDiffForAffected(UseUnitSkill diff, Unit target, float mult, List<IGameDiff> caused);
}

/// <summary>
/// The shape of the skill.
/// </summary>
public record UnitSkillShape(params (Vector2Int offset, float multiplier)[] Points) {
    public static UnitSkillShape Single { get; } = new((new(0, 0), 1)) { Rotatable = false };
    
    public bool Rotatable { get; init; } = true;
    [JsonIgnore] public int NPoints => Points.Length;

    public Unit? HitsAnyUnit(GameState gs, Node target, int rotation) {
        foreach (var (offset, _) in Points)
            if (gs.TryNodeAt(target.Index + offset.Rotate(rotation))?.Unit is {} u)
                return u;
        return null;
    }

    public void CreateDiffForEach(GameState gs, UseUnitSkill diff, List<IGameDiff> caused) {
        //foreach (var u in gs.Units.Except(new[]{diff.Unit}))
        foreach (var (offset, mult) in Points)
            if (gs.TryNodeAt(diff.Target.Index + offset.Rotate(diff.Rotation))?.Unit is { } u)
                diff.Skill.CreateDiffForAffected(diff, u, mult, caused);
    }
}

//[JsonConverter(typeof(SingletonConverter<UnitSkill>))]
public abstract class UnitSkill: IUnitSkill {
    public LString Name { get; }
    public abstract LString Description { get; }
    public int MinRange { get; }
    public int MaxRange { get; }
    public virtual UnitSkillShape Shape => UnitSkillShape.Single;
    public virtual UnitSkillType Type => UnitSkillType.Attack;

    UnitSkill(LString Name, int MinRange, int MaxRange) {
        this.Name = Name;
        this.MinRange = MinRange;
        this.MaxRange = MaxRange;
    }

    /// <inheritdoc/>
    public virtual void PreApply(GameState gs, UseUnitSkill diff, List<IGameDiff> caused) =>
        Shape.CreateDiffForEach(gs, diff, caused);
    /// <inheritdoc/>
    public virtual void Apply(GameState gs, UseUnitSkill diff, List<IGameDiff> caused) { }
    /// <inheritdoc/>
    public virtual void Unapply(GameState gs, UseUnitSkill diff) { }
    /// <inheritdoc/>
    public abstract void CreateDiffForAffected(UseUnitSkill diff, Unit target, float mult, List<IGameDiff> caused);

    //note: you should return a ReduceUnitHP even if the damage is 0, as
    // the ReduceUnitHP is inspected by attack animations
    public static IGameDiff DiffForAttack(UseUnitSkill diff, Unit target, float mult, int power) {
        var baseDmg = diff.Unit.Stats.Atk + power;
        return new ReduceUnitHP(target, Math.Max(1, (int)MathF.Ceiling(mult * (baseDmg - target.Stats.Def))));
    }

    public LString DescriptionForAttack(int power) => $"Power: {power}";

    public LString DescriptionForMod(IStatModDefinition mod) => mod switch {
        AdditiveStatMod.Definition a => $"{a.BaseMod.AsDelta()} {a.Stat.Abbrev()} for {a.Duration} turns" +
                                        ((a.ChangePerTurn == 0) ? "" : $"; {a.ChangePerTurn.AsDelta()} per turn"),
        _ => throw new NotImplementedException(mod.GetType().RName())
    };
    
    public abstract class BasicAttack : UnitSkill, IUnitSkill {
        protected readonly int power;
        public BasicAttack(LString Name, int MinRange, int MaxRange, int Power) : base(Name, MinRange, MaxRange) {
            power = Power;
        }

        public override void CreateDiffForAffected(UseUnitSkill diff, Unit target, float mult, List<IGameDiff> caused) {
            caused.Add(DiffForAttack(diff, target, mult, power));
        }
    }

    public class ReimuNeedles : BasicAttack {
        public override LString Description { get; }

        private ReimuNeedles() : base("Needles", 8, 9, 10) {
            Description = $"Basic ranged attack.\n{DescriptionForAttack(power)}";
        }
        public static ReimuNeedles S { get; } = new();
    }
    public class ReimuDebuff : UnitSkill {
        public override LString Description { get; }
        private ReimuDebuff() : base("Fantasy Seal", 3, 7) {
            Mod = new AdditiveStatMod.Definition(Stat.Defense, -16, 5, 2, this);
            Description = $"Basic debuff.\n{DescriptionForMod(Mod)}";
            
        }
        public static ReimuDebuff S { get; } = new();
        public IStatModDefinition Mod { get; }
        
        public override void CreateDiffForAffected(UseUnitSkill diff, Unit target, float mult, List<IGameDiff> caused) {
            caused.Add(Mod.Create(target.State, target));
        }
    }
    public class ReimuBuff : UnitSkill {
        public override LString Description { get; }
        private ReimuBuff() : base("Hakurei Barrier", 3, 7) {
            Mod = new AdditiveStatMod.Definition(Stat.Attack, 4, -2, 2, this);
            Description = $"Basic debuff.\n{DescriptionForMod(Mod)}";
        }
        public static ReimuBuff S { get; } = new();
        public IStatModDefinition Mod { get; }
        
        public override void CreateDiffForAffected(UseUnitSkill diff, Unit target, float mult, List<IGameDiff> caused) {
            caused.Add(Mod.Create(target.State, target));
        }
    }

    public class MarisaBroom : BasicAttack {
        public override LString Description { get; }
        private MarisaBroom() : base("Broom", 1, 1, 8) {
            Description = $"Basic melee attack.\n{DescriptionForAttack(power)}";
        }
        public static MarisaBroom S { get; } = new();
    }

    public class YukariGapPower : BasicAttack {
        public override LString Description { get; }
        private YukariGapPower() : base("Gap Power", 1, 1, 4) { 
            Description = $"Basic melee attack.\n{DescriptionForAttack(power)}";
        }
        public static YukariGapPower S { get; } = new();
    }
}


}