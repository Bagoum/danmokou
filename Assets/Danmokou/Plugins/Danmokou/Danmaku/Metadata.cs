using System;
using System.Linq;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.GameInstance;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using Danmokou.SM;
using UnityEngine;
using static Danmokou.Core.LocalizedStrings.CDifficulty;


namespace Danmokou.Danmaku {
//This class is effectively readonly but due to JSONify requirements, any field must not be readonly. 
[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class DifficultySettings {
    /// <summary>
    /// Inclusive
    /// </summary>
    public const int MIN_SLIDER = 0;
    /// <summary>
    /// Inclusive
    /// </summary>
    public const int MAX_SLIDER = 42;
    public const int DEFAULT_SLIDER = 18;
    public FixedDifficulty? standard;
    public int customValueSlider;
    //Cached to avoid rechecking the difficulty list every op 
    private FixedDifficulty customStandard;
    public int? customRank;
    public int numSuicideBullets;
    public double playerDamageMod;
    
    public double bossHPMod;
    public bool respawnOnDeath;
    public double faithDecayMultiplier;
    public double faithAcquireMultiplier;
    public double meterUsageMultiplier;
    public double meterAcquireMultiplier;
    public bool bombsEnabled;
    public bool meterEnabled;
    public double playerSpeedMultiplier;
    public double playerHitboxMultiplier;
    public double playerGrazeboxMultiplier;
    public double pocOffset;
    public int? startingLives;
    [JsonIgnore] [ProtoIgnore] 
    public FixedDifficulty ApproximateStandard => standard ?? customStandard;
    [JsonIgnore] [ProtoIgnore]
    private float CustomValue => DifficultyForSlider(customValueSlider);
    [JsonIgnore] [ProtoIgnore]
    public float Value => standard?.Value() ?? CustomValue;
    [JsonIgnore] [ProtoIgnore]
    public float Counter => ApproximateStandard.Counter();
    [JsonIgnore] [ProtoIgnore] 
    public (int min, int max) RankLevelBounds => ApproximateStandard.RankLevelBounds();
    
    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
    public DifficultySettings() : this(FixedDifficulty.Normal) { }
    public DifficultySettings(FixedDifficulty standard) : this((FixedDifficulty?)standard) { }

    public void SetCustomDifficulty(int value) {
        customValueSlider = value;
        customStandard = Nearest(customValueSlider);
    }
    public DifficultySettings(FixedDifficulty? standard, int slider=DEFAULT_SLIDER, int? rank = null, 
        int numSuicideBullets = 0,
        double playerDamageMod=1f, float bulletSpeedMod=1f, double bossHPMod=1f, bool respawnOnDeath = false, 
        double faithDecayMult=1, double faithAcquireMult=1, double meterUsageMult=1, double meterAcquireMult=1, 
        bool bombsEnabled=true, bool meterEnabled=true, 
        float playerSpeedMult=1f, float playerHitboxMult=1f, float playerGrazeboxMult=1f,
        float pocOffset=0, int? startingLives=null
        ) {
        this.standard = standard;
        this.customRank = rank;
        SetCustomDifficulty(slider);
        this.numSuicideBullets = numSuicideBullets;
        this.playerDamageMod = playerDamageMod;
        this.bossHPMod = bossHPMod;
        this.respawnOnDeath = respawnOnDeath;
        this.faithDecayMultiplier = faithDecayMult;
        this.faithAcquireMultiplier = faithAcquireMult;
        this.meterUsageMultiplier = meterUsageMult;
        this.meterAcquireMultiplier = meterAcquireMult;
        this.bombsEnabled = bombsEnabled;
        this.meterEnabled = meterEnabled;
        this.playerSpeedMultiplier = playerSpeedMult;
        this.playerHitboxMultiplier = playerHitboxMult;
        this.playerGrazeboxMultiplier = playerGrazeboxMult;
        this.pocOffset = pocOffset;
        this.startingLives = startingLives;
    }

    public static FixedDifficulty Nearest(int slider) {
        float d = DifficultyForSlider(slider);
        return GameManagement.VisibleDifficulties.OrderBy(fd => Mathf.Abs(fd.Value() - d)).First();
    }

    public static float DifficultyForSlider(int slider) => Mathf.Pow(2, (slider - 12f) / 12f);

    public static LString FancifySlider(int slider) {
        float d = DifficultyForSlider(slider);
        //requires ordering on VisibleDifficulties
        var fds = GameManagement.VisibleDifficulties.OrderBy(fd => fd.Value()).ToArray();
        for (int ii = 0; ii < fds.Length; ++ii) {
            var fd = fds[ii];
            if (Mathf.Abs(d / fd.Value() - 1) < 0.01) {
                return desc_effective_exact_ls(fd.Describe());
            } else if (d < fd.Value()) {
                if (ii == 0) return desc_effective_less_ls(fd.Describe());
                var ratio1 = Mathf.Log(fd.Value() / d);
                var ratio2 = Mathf.Log(d / fds[ii - 1].Value());
                var ratio = ratio2 / (ratio1 + ratio2);
                var percent = (int) (ratio * 100);
                return desc_effective_ratio_ls(100 - percent, fds[ii - 1].Describe(), percent, fd.Describe());
            } 
        }
        return desc_effective_more_ls(fds[fds.Length - 1].Describe());
    }

    public string Describe() => standard?.Describe() ?? $"CUST:{customValueSlider:00}";
    /// <summary>
    /// For filenames
    /// </summary>
    public string DescribeSafe() => standard?.Describe() ?? $"CUST{customValueSlider:00}";
    public string DescribePadR() => Describe().PadRight(7);
}
public readonly struct SMRunner {
    public readonly StateMachine? sm;
    public readonly ICancellee cT;
    public readonly bool cullOnFinish;
    private readonly bool root;

    /// <summary>
    /// When a summon (root:False) fires another summon, the nested summon should
    /// only depend on the cancellation of the phase-declaring boss/stage.
    /// <br/>Thus, the local cancellation information of summons is destroyed when
    /// using MakeNested to derive a nested summon's token.
    /// <br/>Note that using PhaseSM or PatternSM on the summon will make the nested
    /// summon dependent on the summon's cancellation. This is because cT here will
    /// instead point to the JointCancellee constructed in PhaseSM or PatternSM,
    /// which will not discard information.
    /// </summary>
    public ICancellee MakeNested(ICancellee local) =>
        root ?
            new JointCancellee(cT, local) :
            (ICancellee)new PassthroughCancellee(cT.Root, local);
    
    private readonly GenCtx? gcx;
    public GenCtx? NewGCX => gcx?.Copy();

    public static SMRunner Null => new SMRunner(null, Cancellable.Null, false, false, null);
    public static SMRunner Cull(StateMachine? sm, ICancellee cT, GenCtx? gcx=null) => new SMRunner(sm, cT, true, false, gcx);
    /// <summary>
    /// Use over CULL when any nested summons should be bounded by this object's cancellation (ie. bosses).
    /// </summary>
    public static SMRunner CullRoot(StateMachine? sm, ICancellee cT, GenCtx? gcx=null) => new SMRunner(sm, cT, true, true, gcx);
    public static SMRunner Run(StateMachine? sm, ICancellee cT, GenCtx? gcx=null) => new SMRunner(sm, cT, false, false, gcx);
    public static SMRunner RunNoCancelRoot(StateMachine? sm) => new SMRunner(sm, Cancellable.Null, false, true, null);
    public SMRunner(StateMachine? sm, ICancellee cT, bool cullOnFinish, bool root, GenCtx? gcx) {
        this.sm = sm;
        this.cT = cT;
        this.cullOnFinish = cullOnFinish;
        this.gcx = gcx;
        this.root = root;
    }
}


/// <summary>
/// Captures some game information at the beginning of a phase for comparison in PhaseCompletion.
/// </summary>
public readonly struct CampaignSnapshot {
    public readonly int hitsTaken;
    public readonly int meterFrames;
    public readonly int frame;
    public readonly int bombsUsed;
    public CampaignSnapshot(InstanceData data) {
        hitsTaken = data.HitsTaken;
        meterFrames = data.MeterFrames;
        frame = ETime.FrameNumber;
        bombsUsed = data.BombsUsed;
    }
}

public readonly struct PhaseCompletion {
    public readonly PhaseContext phase;
    public readonly PhaseClearMethod clear;
    public readonly BehaviorEntity exec;
    public readonly int hits;
    public bool NoHits => hits == 0;
    public readonly bool noMeter;
    public readonly bool noBombs;
    private readonly int elapsedFrames;
    public float ElapsedTime => elapsedFrames / ETime.ENGINEFPS_F;
    //The props timeout may be overriden
    private readonly float timeout;
    private float ElapsedRatio => timeout > 0 ? ElapsedTime / timeout : 0;
    private const float ELAPSED_YIELD = 0.58f;
    private float ElapsedItemMultiplier => phase.Props.phaseType == PhaseType.TIMEOUT ? 1 : 
        Mathf.Lerp(1, 0.27183f, (ElapsedRatio - ELAPSED_YIELD) / (1 - ELAPSED_YIELD));
    private float ItemMultiplier => phase.Props.cardValueMult * ElapsedItemMultiplier;

    public string Performance {
        get {
            if (PerfectCaptured == true)
                return "Perfect Capture!!";
            if (Captured == true)
                return "Card Capture!";
            if (Cleared == true)
                return "Card Clear";
            return "Card Failed...";
        }
    }

    public const int MaxCaptureStars = 3;
    public int? CaptureStars {
        get {
            if (!StandardCardFinish) 
                return null;
            if (PerfectCaptured == true) 
                return MaxCaptureStars;
            if (Captured == true) 
                return 2;
            if (Cleared == true) 
                return 1;
            return 0;
        }
    }
    /// <summary>
    /// True if the card was perfectly captured. False if it was not perfectly captured.
    /// Null if there was no card at all (eg. minor enemies or cancellation).
    /// <br/>A perfect capture requires capturing the card without using any meter.
    /// </summary>
    public bool? PerfectCaptured => Captured.And(noMeter);
    
    /// <summary>
    /// True if the card was captured. False if it was not captured.
    /// Null if there was no card at all (eg. minor enemies or cancellation).
    /// <br/>A capture requires clearing the card, taking no hits, and not bombing. 
    /// </summary>
    public bool? Captured => Cleared.And(NoHits).And(noBombs);

    /// <summary>
    /// True if the card was cleared. False if it was not cleared.
    /// Null if there was no card at all (eg. minor enemies or cancellation).
    /// <br/>A clear requires draining all the boss HP or waiting out a timeout (timeouts require no hits).
    /// </summary>
    public bool? Cleared => StandardCardFinish ?
        (bool?) ((clear.Destructive()) ||
                 //For timeouts, clearing requires no-hit
                 (phase.Props.phaseType == PhaseType.TIMEOUT && clear == PhaseClearMethod.TIMEOUT && NoHits))
        : null;

    /// <summary>
    /// True if the phase is a card-type phase and it was not externally cancelled.
    /// </summary>
    public bool StandardCardFinish => (phase.Props.phaseType?.IsCard() ?? false) && clear != PhaseClearMethod.CANCELLED;

    private ItemDrops DropPerfectCapture => new ItemDrops(42, 7, 42, 0, 20, true).Mul(ItemMultiplier);
    private ItemDrops DropCapture => new ItemDrops(42, 0, 42, 0, 20, true).Mul(ItemMultiplier);
    private ItemDrops DropClear => new ItemDrops(29, 0, 13, 0, 13, true).Mul(ItemMultiplier);
    private ItemDrops DropNoHit => new ItemDrops(0, 0, 37, 0, 13, true).Mul(ItemMultiplier);

    public ItemDrops? DropItems {
        get {
            if (GameManagement.Instance.mode.DisallowCardItems()) return null;
            if (PerfectCaptured == true) return DropPerfectCapture;
            if (Captured == true) return DropCapture;
            else if (Cleared == true) return DropClear;
            else if (NoHits) return DropNoHit;
            return null;
        }
    }

    public PhaseCompletion(PhaseContext phase, PhaseClearMethod clear, BehaviorEntity exec, CampaignSnapshot snap, float timeout) {
        this.phase = phase;
        this.clear = clear;
        this.exec = exec;
        this.timeout = timeout;
        this.hits = GameManagement.Instance.HitsTaken - snap.hitsTaken;
        this.noMeter = GameManagement.Instance.MeterFrames == snap.meterFrames;
        this.noBombs = GameManagement.Instance.BombsUsed == snap.bombsUsed;
        this.elapsedFrames = ETime.FrameNumber - snap.frame;
    }
}
}