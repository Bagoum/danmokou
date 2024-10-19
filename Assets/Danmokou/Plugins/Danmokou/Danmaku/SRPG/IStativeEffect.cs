using System;
using System.Collections.Generic;
using Danmokou.SRPG.Diffs;

namespace Danmokou.SRPG {
public interface IStativeEffect {
    /// <summary>
    /// For a game action `source` which was just applied, return a game action
    ///  representing any changes caused by the interaction of this entity with the action.
    /// </summary>
    public IGameDiff? ProcessCausation(IGameDiff source);
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
    int ApplyMod(Stat type, int val);
}
}