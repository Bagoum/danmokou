using System;
using UnityEngine;

namespace Danmaku {
public static class Enums {
    /// <summary>
    /// Handling for setting the firing index. The generic repeaters use DEFER by default.
    /// </summary>
    public enum Parametrization {
        /// <summary>
        /// Use this function's indexing.
        /// </summary>
        THIS = 0,
        /// <summary>
        /// Use the parent function's indexing.
        /// </summary>
        DEFER = 1,
        /// <summary>
        /// Combine the indexing. Use p1 (parent) p2 (this) in FXY functions.
        /// </summary>
        ADDITIVE = 2,
        /// <summary>
        /// Combine the indexing around the repeat number of the child function.
        /// Use p1m {RPT} p2m {RPT} in FXY functions.
        /// </summary>
        MOD = 3,
        /// <summary>
        /// Combine the indexing around the repeat number of the child function.
        /// The ordering is inverted, so the first summon has local index RPT-1 and the last has local index 0.
        /// Use p1m {RPT} p2m {RPT} in FXY functions.
        /// </summary>
        INVMOD = 4
    }
    
    /// <summary>
    /// Handling for setting the angle when using an SAOffset command.
    /// </summary>
    public enum SAAngle {
        /// <summary>
        /// (o) The angle from the parent is not modified.
        /// If you add an angle offset, the entire summon will rotate.
        /// </summary>
        ORIGINAL,
        /// <summary>
        /// (bo) The angle from the parent is banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        ORIGINAL_BANK,
        /// <summary>
        /// (br) The angle is set to the position of the summon, then banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        REL_ORIGIN_BANK,
        /// <summary>
        /// (bt) The angle is set to the tangent on the summon curve, then banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        TANGENT_BANK
    }
    /// <summary>
    /// Enum to describe an operation that may optionally block.
    /// </summary>
    public enum Blocking {
        /// <summary>
        /// The operation is blocking and must finish before the next operation.
        /// </summary>
        BLOCKING,
        /// <summary>
        /// The operation is nonblocking; the next operation may begin immediately.
        /// </summary>
        NONBLOCKING
    }
    
    
    /// <summary>
    /// Enum that describe components of a V2RV2 location.
    /// Used for targeting.
    /// </summary>
    public enum RV2ControlMethod {
        /// <summary>
        /// (nx) Nonrotational x
        /// </summary>
        NX,
        /// <summary>
        /// (ny) Nonrotational y
        /// </summary>
        NY,
        /// <summary>
        /// (rx) Rotational x
        /// </summary>
        RX,
        /// <summary>
        /// (ry) Rotational y
        /// </summary>
        RY,
        /// <summary>
        /// (a) Angle. When targeted, rotates the entire summon.
        /// </summary>
        ANG,
        /// <summary>
        /// (ra) Angle. When targeted, only adds to the RV2 angle.
        /// </summary>
        RANG
    }
    

    /// <summary>
    /// Enum describing the direction in which a bullet is fired from a parent.
    /// </summary>
    public enum Facing {
        /// <summary>
        /// Starts from original_angle. This is zero except for summons/bullets,
        /// for which it is set to the V2RV2 angle of the summon.
        /// </summary>
        ORIGINAL,
        /// <summary>
        /// Starts from 0.
        /// </summary>
        DEROT, 
        /// <summary>
        /// Starts from the angle of the last nonzero velocity delta.
        /// </summary>
        VELOCITY, 
        /// <summary>
        /// Starts from original_angle + the velocity direction.
        /// </summary>
        ROTVELOCITY
    }
    

    /// <summary>
    /// Enum describing the type of a phase within a pattern script.
    /// </summary>
    public enum PhaseType {
        /// <summary>
        /// Nonspells.
        /// </summary>
        NONSPELL,
        /// <summary>
        /// Spells. Automatically summons a spell cutin and spell circle if applicable.
        /// </summary>
        SPELL,
        /// <summary>
        /// Timeouts. Same as SPELL, but also sets the HP to infinity and gives full rewards for timing out.
        /// </summary>
        TIMEOUT,
        /// <summary>
        /// Final spell. Same as SPELL, but doesn't drop value items on completion.
        /// </summary>
        FINAL,
        /// <summary>
        /// Dialogue section. The score multiplier is granted lenience and the executor is granted infinite health during this.
        /// </summary>
        DIALOGUE,
        /// <summary>
        /// Standard stage section. Only use in stage scripts.
        /// </summary>
        STAGE,
        /// <summary>
        /// A stage section wrapping a midboss summon. Only use in stage scripts.
        /// </summary>
        STAGEMIDBOSS,
        /// <summary>
        /// A stage section wrapping an endboss summon. Only use in stage scripts.
        /// </summary>
        STAGEENDBOSS
    }
    
    public enum DifficultySet {
        Easier = 10,
        Easy = 20,
        Normal = 30,
        Hard = 40,
        Lunatic = 50,
        Ultra = 60,
        Abex = 70,
        Assembly = 80,
    }
    public enum CampaignMode {
        NULL,
        MAIN,
        STAGE_PRACTICE,
        CARD_PRACTICE,
        TUTORIAL,
        SCENE_CHALLENGE,
    }

    public enum Layer {
        LowProjectile,
        HighProjectile
    }

    public static bool IsStageBoss(this PhaseType st) => st == PhaseType.STAGEENDBOSS || st == PhaseType.STAGEMIDBOSS;
    public static bool IsPattern(this PhaseType st) => st.IsCard() || st.IsStage();
    public static bool IsLenient(this PhaseType st) => st == PhaseType.DIALOGUE;
    public static bool IsStage(this PhaseType st) => st == PhaseType.STAGE;
    public static bool RequiresHPGuard(this PhaseType st) => st == PhaseType.DIALOGUE || st.IsCard();
    public static bool RequiresFullHPBar(this PhaseType st) => st == PhaseType.FINAL || st == PhaseType.TIMEOUT;
    public static bool IsCard(this PhaseType st) => st == PhaseType.NONSPELL || st.IsSpell();
    public static bool IsSpell(this PhaseType st) =>
        st == PhaseType.SPELL || st == PhaseType.TIMEOUT || st == PhaseType.FINAL;
    public static PhaseType Invert(this PhaseType st) {
        if (st.IsSpell()) return PhaseType.NONSPELL;
        if (st == PhaseType.NONSPELL) return PhaseType.SPELL;
        return st;
    }

    public static float? HPBarLength(this PhaseType st) {
        if (st.IsSpell()) return 1f;
        if (st == PhaseType.NONSPELL) return 0.5f;
        return null;
    }
    public static int? DefaultHP(this PhaseType st) {
        if (st == PhaseType.TIMEOUT || st == PhaseType.DIALOGUE) return 1000000000;
        return null;
    }
    public static int Int(this Layer l) {
        if (l == Layer.LowProjectile) return LayerMask.NameToLayer("LowProjectile");
        else  return LayerMask.NameToLayer("HighProjectile");
    }

    public static float Value(this DifficultySet d) {
        if (d == DifficultySet.Easier) return 0.707f;       // 2^-0.5
        else if (d == DifficultySet.Easy) return 1.00f;     // 2^0.0
        else if (d == DifficultySet.Normal) return 1.414f;  // 2^0.5
        else if (d == DifficultySet.Hard) return 2.00f;     // 2^1.0
        else if (d == DifficultySet.Lunatic) return 2.828f; // 2^1.5
        else if (d == DifficultySet.Ultra) return 3.732f;   // 2^1.9
        else if (d == DifficultySet.Abex) return 4.925f;    // 2^2.3
        else if (d == DifficultySet.Assembly) return 6.063f;// 2^2.6
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }
    public static float Counter(this DifficultySet d) {
        if (d == DifficultySet.Easier) return 0;
        else if (d == DifficultySet.Easy) return 1;
        else if (d == DifficultySet.Normal) return 2;
        else if (d == DifficultySet.Hard) return 3;
        else if (d == DifficultySet.Lunatic) return 4;
        else if (d == DifficultySet.Ultra) return 5;
        else if (d == DifficultySet.Abex) return 6;
        else if (d == DifficultySet.Assembly) return 7;
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }

    public static string Describe(this DifficultySet d) {
        if (d == DifficultySet.Easier) return "Easier";
        else if (d == DifficultySet.Easy) return "Easy";
        else if (d == DifficultySet.Normal) return "Normal";
        else if (d == DifficultySet.Hard) return "Hard";
        else if (d == DifficultySet.Lunatic) return "Lunatic";
        else if (d == DifficultySet.Ultra) return "Ultra";
        else if (d == DifficultySet.Abex) return "Abex";
        else if (d == DifficultySet.Assembly) return "Assembly";
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }

    public static string DescribePadR(this DifficultySet d) => d.Describe().PadRight(8);

    public static bool IsOneCard(this CampaignMode mode) =>
        mode == CampaignMode.CARD_PRACTICE || mode == CampaignMode.SCENE_CHALLENGE;
    public static bool OneLife(this CampaignMode mode) => mode.IsOneCard();
    public static bool DisallowItems(this CampaignMode mode) => mode.IsOneCard();
    public static bool PreserveReloadAudio(this CampaignMode mode) => mode.IsOneCard();

}
}