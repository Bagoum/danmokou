using System;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using Danmokou.SRPG.Diffs;

namespace Danmokou.SRPG {
public interface IStativeEffect {
    /// <summary>
    /// For a game diff `source` which was just applied, if this entity interacts with `diff`,
    ///  then add any changes caused by this interaction into `caused`.
    /// </summary>
    public void ProcessCausation(IGameDiff source, List<IGameDiff> caused);
}

public enum Stat {
    MaxHP,
    CurrHP,
    Attack,
    Defense,
    Speed,
    Move
}

public record StatBlock(int BMaxHP, int BAtk, int BDef, int BSpd, int BMove) {
    public int CurrHP { get; set; } = BMaxHP;
    public int MaxHP => AccStat(Stat.MaxHP, BMaxHP);
    public int Atk => AccStat(Stat.Attack, BAtk);
    public int Def => AccStat(Stat.Defense, BDef);
    public int Spd => AccStat(Stat.Speed, BSpd);
    public int Move => AccStat(Stat.Move, BMove);
    public List<IStatMod> Mods { get; } = new();

    public int BaseStat(Stat type) => type switch {
        Stat.MaxHP => BMaxHP,
        Stat.CurrHP => CurrHP,
        Stat.Attack => BAtk,
        Stat.Defense => BDef,
        Stat.Speed => BSpd,
        Stat.Move => BMove,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public int EffectiveStat(Stat type) => AccStat(type, BaseStat(type));

    private int AccStat(Stat type, int baseVal) {
        if (type == Stat.CurrHP) return baseVal;
        foreach (var mod in Mods)
            baseVal = mod.ApplyMod(type, baseVal);
        return baseVal;
    }
}

public interface IStatMod : IStativeEffect {
    Unit Target { get; }
    UnitSkill Source { get; }
    int ApplyMod(Stat type, int val);

    public record Create(IStatMod Mod) : IUnitXUnitGameDiff {
        public Unit Target => Mod.Target;
        public IGameDiff? CausedBy { get; set; }
        private int Index { get; set; }

        public void Apply(GameState gs, List<IGameDiff> caused) {
            Index = Mod.Target.Stats.Mods.Count;
            Mod.Target.Stats.Mods.Insert(Index, Mod);
        }

        public void Unapply(GameState gs) {
            Mod.Target.Stats.Mods.RemoveAt(Index);
        }
    }

    public record Delete(IStatMod Mod) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        private int Index { get; set; }

        public void Apply(GameState gs, List<IGameDiff> caused) {
            Index = Mod.Target.Stats.Mods.IndexOf(Mod);
            Mod.Target.Stats.Mods.RemoveAt(Index);
        }

        public void Unapply(GameState gs) {
            Mod.Target.Stats.Mods.Insert(Index, Mod);
        }
    }
}

public interface IStatModDefinition {
    protected IStatMod CreateInstance(GameState gs, Unit target);
    IGameDiff Create(GameState gs, Unit target) => new IStatMod.Create(CreateInstance(gs, target));
}

public class AdditiveStatMod : IStatMod {
    public record Definition(Stat Stat, int BaseMod, int ChangePerTurn, int Duration, UnitSkill Source)
        : IStatModDefinition {
        IStatMod IStatModDefinition.CreateInstance(GameState gs, Unit target) =>
            new AdditiveStatMod(this, target, gs.TurnNumber + Duration, gs.ActingFactionIdx);
    }
    public Definition Defn { get; }
    public Unit Target { get; }
    public UnitSkill Source => Defn.Source;
    private int currMod;
    public int EndOnTurn { get; }
    public int FactionIdx { get; }

    public AdditiveStatMod(Definition defn, Unit target, int endOnTurn, int factionIdx) {
        Defn = defn;
        Target = target;
        currMod = Defn.BaseMod;
        EndOnTurn = endOnTurn;
        FactionIdx = factionIdx;
    }

    public int ApplyMod(Stat type, int val) => type == Defn.Stat ? (val + currMod) : val;

    public void ProcessCausation(IGameDiff source, List<IGameDiff> caused) {
        if (source is GameState.SwitchFactionTurn sft && sft.NextIdx == FactionIdx) {
            if (sft.TurnNo >= EndOnTurn)
                caused.Add(new IStatMod.Delete(this));
            else
                caused.Add(new Update(this));
        }
    }

    public record Update(AdditiveStatMod Mod) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        
        public void Apply(GameState gs, List<IGameDiff> caused) {
            Mod.currMod += Mod.Defn.ChangePerTurn;
        }

        public void Unapply(GameState gs) {
            Mod.currMod -= Mod.Defn.ChangePerTurn;
        }
    }
}

}