using System;
using System.Collections.Generic;
using BagoumLib.Culture;
using Danmokou.GameInstance;
using Danmokou.Services;
using UnityEngine;
using static Danmokou.Core.LocalizedStrings.UI;

namespace Danmokou.Core {
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
    /// If the local index of the child function goes past the repeat number, it will wrap back to zero.
    /// Use p1m {RPT} p2m {RPT} in FXY functions.
    /// </summary>
    MOD = 3,
    /// <summary>
    /// Combine the indexing around the repeat number of the child function.
    /// The ordering is inverted, so the first summon has local index RPT-1 and the last has local index 0.
    /// If the local index of the child function goes past the repeat number, it will wrap back to RPT-1.
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
    /// Stage section where graphics, but no enemies/bullets, appear on screen.
    /// </summary>
    ANNOUNCE,
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

public enum FixedDifficulty {
    Easy = 20,
    Normal = 30,
    Hard = 40,
    Lunatic = 50
}
public enum InstanceMode {
    /// <summary>
    /// Used when the instance is not active (eg. on the main menu).
    /// NULL is the mode used when the program starts.
    /// A player may still exist (eg. shot demo) and make use of InstanceData, but many features may be disabled.
    /// </summary>
    NULL,
#if UNITY_EDITOR || ALLOW_RELOAD
    /// <summary>
    /// Used when there is no real instance, but there is still full gameplay (primarily debugging situations with reload).
    /// Using local reset by pressing R will set the mode to DEBUG.
    /// As the default mode is NULL, you may need to press R once in debugging situations to get full functionality.
    /// </summary>
    DEBUG,
#endif
    /// <summary>
    /// Playing through a full campaign (eg. 6 sequential stages). Note that EX stages are distinct campaigns.
    /// </summary>
    CAMPAIGN,
    /// <summary>
    /// Stage practice mode, which starts on one stage section and plays until the end of the stage.
    /// </summary>
    STAGE_PRACTICE,
    /// <summary>
    /// Boss card practice mode, which plays exactly one card.
    /// </summary>
    BOSS_PRACTICE,
    /// <summary>
    /// Tutorial.
    /// </summary>
    TUTORIAL,
    /// <summary>
    /// Scene challenge mode (STB-style), which plays exactly one card.
    /// </summary>
    SCENE_CHALLENGE,
}

public enum Layer {
    LowProjectile,
    HighProjectile,
    LowFX,
    HighFX
}

public enum PhaseClearMethod {
    HP,
    PHOTO,
    TIMEOUT,
    CANCELLED
}


public enum Vulnerability {
    VULNERABLE,
    NO_DAMAGE,
    PASS_THROUGH
}

public enum ItemType {
    /// <summary>
    /// Value item (blue)
    /// </summary>
    VALUE,
    /// <summary>
    /// Small value item (blue)
    /// </summary>
    SMALL_VALUE,
    /// <summary>
    /// Point++ item (green)
    /// </summary>
    PPP,
    /// <summary>
    /// Life item (pink)
    /// </summary>
    LIFE,
    /// <summary>
    /// Power item (orange)
    /// </summary>
    POWER,
    /// <summary>
    /// Full power item (yellow)
    /// </summary>
    FULLPOWER,
    /// <summary>
    /// 1-up item (green mushroom)
    /// </summary>
    ONEUP,
    /// <summary>
    /// Special meter refill item (rotating yellow prism)
    /// </summary>
    GEM,
    /// <summary>
    /// Powerup item that switches between modes D/M/K
    /// </summary>
    POWERUP_SHIFT,
    /// <summary>
    /// Powerup item always in mode D
    /// </summary>
    POWERUP_D,
    /// <summary>
    /// Powerup item always in mode M
    /// </summary>
    POWERUP_M,
    /// <summary>
    /// Powerup item always in mode K
    /// </summary>
    POWERUP_K
}

public enum Subshot {
    TYPE_D = 0,
    TYPE_M = 1,
    TYPE_K = 2
}

public static class EnumHelpers2 {
    public static readonly IReadOnlyList<Subshot> Subshots = new[] {Subshot.TYPE_D, Subshot.TYPE_M, Subshot.TYPE_K};

    public static bool IsStageBoss(this PhaseType st) => st == PhaseType.STAGEENDBOSS || st == PhaseType.STAGEMIDBOSS;
    public static bool IsPattern(this PhaseType st) => st.IsCard() || st.IsStage();
    public static bool IsLenient(this PhaseType st) => st == PhaseType.DIALOGUE || st == PhaseType.ANNOUNCE;
    public static bool IsStage(this PhaseType st) => st == PhaseType.STAGE;
    public static bool AppearsInPractice(this PhaseType st) => st != PhaseType.ANNOUNCE;
    public static bool RequiresHPGuard(this PhaseType st) => st.IsLenient() || st.IsCard();

    public static Vulnerability? DefaultVulnerability(this PhaseType st) =>
        st == PhaseType.TIMEOUT ? Vulnerability.PASS_THROUGH :
        st.RequiresHPGuard() ? Vulnerability.NO_DAMAGE : (Vulnerability?)null;
    public static bool RequiresFullHPBar(this PhaseType st) => st == PhaseType.FINAL || st == PhaseType.TIMEOUT;
    public static bool IsCard(this PhaseType st) => st == PhaseType.NONSPELL || st.IsSpell();
    public static bool IsSpell(this PhaseType st) =>
        st == PhaseType.SPELL || st == PhaseType.TIMEOUT || st == PhaseType.FINAL;

    public static bool HideTimeout(this PhaseType st) => st == PhaseType.STAGE || st == PhaseType.DIALOGUE;

    public static float? HPBarLength(this PhaseType st) {
        if (st.IsSpell()) return 1f;
        if (st == PhaseType.NONSPELL) return 0.5f;
        return null;
    }
    public static int? DefaultHP(this PhaseType st) {
        if (st == PhaseType.TIMEOUT || st == PhaseType.DIALOGUE) return 1000000000;
        return null;
    }
    public static int Int(this Layer l) => LayerMask.NameToLayer(l switch {
            Layer.LowFX => "LowEffects",
            Layer.HighFX => "TransparentFX",
            Layer.LowProjectile => "LowProjectile",
            _ => "HighProjectile"
        });

    public static float Value(this FixedDifficulty d) => d switch {
            FixedDifficulty.Lunatic => 2.828f, //2^1.5
            FixedDifficulty.Hard => 2.00f,     //2^1.0
            FixedDifficulty.Normal => 1.414f,  //2^0.5
            _ => 1.00f                         //2^0.0
        };

    public static float Counter(this FixedDifficulty d) => d switch {
            FixedDifficulty.Lunatic => 3,
            FixedDifficulty.Hard => 2,
            FixedDifficulty.Normal => 1,
            _ => 0
        };
    
    public static (int min, int max) RankLevelBounds(this FixedDifficulty fd) => fd switch {
        FixedDifficulty.Lunatic => (9, RankManager.maxRankLevel),
        FixedDifficulty.Hard => (6, 35),
        FixedDifficulty.Normal => (3, 29),
        _ => (RankManager.minRankLevel, 23)
    };

    public static int DefaultRank(this FixedDifficulty d) => d switch {
        FixedDifficulty.Lunatic => 22,
        FixedDifficulty.Hard => 15,
        FixedDifficulty.Normal => 9,
        _ => 3
    };

    public static LString Describe(this FixedDifficulty d) => d switch {
            FixedDifficulty.Lunatic => difficulty_lunatic,
            FixedDifficulty.Hard => difficulty_hard,
            FixedDifficulty.Normal => difficulty_normal,
            _ => difficulty_easy
        };

    public static bool IsOneCard(this InstanceMode mode) =>
        mode == InstanceMode.BOSS_PRACTICE || mode == InstanceMode.SCENE_CHALLENGE;
    public static bool OneLife(this InstanceMode mode) => mode.IsOneCard();
    public static bool DisallowCardItems(this InstanceMode mode) => false;//mode.IsOneCard();
    public static bool PreserveReloadAudio(this InstanceMode mode) => mode.IsOneCard();

    public static bool Destructive(this PhaseClearMethod cm) =>
        cm == PhaseClearMethod.HP || cm == PhaseClearMethod.PHOTO;

    public static bool Autocollect(this ItemType t) => t == ItemType.GEM;

    public static string Describe(this Subshot s) =>
        s switch {
            Subshot.TYPE_D => "D",
            Subshot.TYPE_M => "M",
            Subshot.TYPE_K => "K",
            _ => "?"
        };

    public static bool TakesDamage(this Vulnerability v) => v == Vulnerability.VULNERABLE;
    public static bool HitsLand(this Vulnerability v) => v == Vulnerability.VULNERABLE || v == Vulnerability.NO_DAMAGE;
}
}