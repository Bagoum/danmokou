using System;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.GameInstance;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using DMK.SM;
using UnityEngine;


namespace DMK.Danmaku {
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
    public float customCounter;
    public int customValueSlider;
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
    private float CustomValue => DifficultyForSlider(customValueSlider);
    [JsonIgnore] [ProtoIgnore]
    public float Value => standard?.Value() ?? CustomValue;
    [JsonIgnore] [ProtoIgnore]
    public float Counter => standard?.Counter() ?? customCounter;
    
    public DifficultySettings() : this(FixedDifficulty.Normal) { } //JSON constructor
    public DifficultySettings(FixedDifficulty standard) : this((FixedDifficulty?)standard) { }

    public void SetCustomDifficulty(int value) {
        customValueSlider = value;
        customCounter = Nearest(value).Counter();
    }
    public DifficultySettings(FixedDifficulty? standard, int slider=DEFAULT_SLIDER, int numSuicideBullets = 0,
        double playerDamageMod=1f, float bulletSpeedMod=1f, double bossHPMod=1f, bool respawnOnDeath = false, 
        double faithDecayMult=1, double faithAcquireMult=1, double meterUsageMult=1, double meterAcquireMult=1, 
        bool bombsEnabled=true, bool meterEnabled=true, 
        float playerSpeedMult=1f, float playerHitboxMult=1f, float playerGrazeboxMult=1f,
        float pocOffset=0, int? startingLives=null
        ) {
        this.standard = standard;
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

    public static string FancifySlider(int slider) {
        float d = DifficultyForSlider(slider);
        //requires ordering on VisibleDifficulties
        var fds = GameManagement.VisibleDifficulties.OrderBy(fd => fd.Value()).ToArray();
        for (int ii = 0; ii < fds.Length; ++ii) {
            var fd = fds[ii];
            if (Mathf.Abs(d / fd.Value() - 1) < 0.01) {
                return $"Exactly {fd.Describe()}";
            } else if (d < fd.Value()) {
                if (ii == 0) return $"Less than {fd.Describe()}";
                var ratio1 = Mathf.Log(fd.Value() / d);
                var ratio2 = Mathf.Log(d / fds[ii - 1].Value());
                var ratio = ratio2 / (ratio1 + ratio2);
                var percent = (int) (ratio * 100);
                return $"{100 - percent}% {fds[ii - 1].Describe()}, {percent}% {fd.Describe()}";
            } 
        }
        return $"More than {fds[fds.Length - 1].Describe()}";
    }

    public string Describe() => standard?.Describe() ?? $"CUST:{customValueSlider:00}";
    /// <summary>
    /// For filenames
    /// </summary>
    public string DescribeSafe() => standard?.Describe() ?? $"CUST{customValueSlider:00}";
    public string DescribePadR() => Describe().PadRight(8);
}
public readonly struct SMRunner {
    [CanBeNull] public readonly StateMachine sm;
    public readonly ICancellee cT;
    public readonly bool cullOnFinish;
    private readonly bool root;

    public ICancellee MakeNested(ICancellee local) => root ?
        new JointCancellee(cT, local) :
        (ICancellee)new PassthroughCancellee(cT, local);
    [CanBeNull] private readonly GenCtx gcx;
    [CanBeNull] public GenCtx NewGCX => gcx?.Copy();

    public static SMRunner Null => new SMRunner(null, Cancellable.Null, false, false, null);
    public static SMRunner Cull(StateMachine sm, ICancellee cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, true, false, gcx);
    /// <summary>
    /// Use over CULL when any nested summons should be bounded by this object's cancellation (ie. bosses).
    /// </summary>
    public static SMRunner CullRoot(StateMachine sm, ICancellee cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, true, true, gcx);
    public static SMRunner Run(StateMachine sm, ICancellee cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, false, false, gcx);
    public static SMRunner RunNoCancelRoot(StateMachine sm) => new SMRunner(sm, Cancellable.Null, false, true, null);
    public SMRunner(StateMachine sm, ICancellee cT, bool cullOnFinish, bool root, [CanBeNull] GenCtx gcx) {
        this.sm = sm;
        this.cT = cT.Joinable; //child-visible section
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
    public CampaignSnapshot(InstanceData data) {
        hitsTaken = data.HitsTaken;
    }
}

public readonly struct PhaseCompletion {
    public readonly PhaseProperties props;
    public readonly PhaseClearMethod clear;
    public readonly BehaviorEntity exec;
    public readonly bool noHits;
    private readonly float elapsed;
    private float Elapsed => props.phaseType == PhaseType.TIMEOUT ? 0 : elapsed;
    private const float ELAPSED_YIELD = 0.58f;
    private float ElapsedItemMultiplier => Mathf.Lerp(1, 0.27183f, (Elapsed - ELAPSED_YIELD) / (1 - ELAPSED_YIELD));
    private float ItemMultiplier => props.cardValueMult * ElapsedItemMultiplier;
    /// <summary>
    /// True if the card was captured. False if it was not captured.
    /// Null if there was no card at all (eg. minor enemies or cancellation).
    /// <br/>A capture requires clearing the card and taking no hits.
    /// </summary>
    public bool? Captured => Cleared.And(noHits);

    /// <summary>
    /// True if the card was cleared. False if it was not cleared.
    /// Null if there was no card at all (eg. minor enemies or cancellation).
    /// <br/>A clear requires draining all the boss HP or waiting out a timeout.
    /// </summary>
    public bool? Cleared => StandardCardFinish ?
        (bool?) ((clear.Destructive()) ||
                 //For timeouts, clearing requires no-hit
                 (props.phaseType == PhaseType.TIMEOUT && clear == PhaseClearMethod.TIMEOUT && noHits))
        : null;

    /// <summary>
    /// True if the phase is a card-type phase and it was not externally cancelled.
    /// </summary>
    public bool StandardCardFinish => (props.phaseType?.IsCard() ?? false) && clear != PhaseClearMethod.CANCELLED;

    private ItemDrops DropCapture => new ItemDrops(42, 0, 42, 0, 20, true).Mul(ItemMultiplier);
    //Final spells give no items if not captured, this is because some final spells have infinite timers
    private ItemDrops DropClear => new ItemDrops(
        props.phaseType == PhaseType.FINAL ? 0 : 29, 0, 13, 0, 13, true).Mul(ItemMultiplier);
    private ItemDrops DropNoHit => new ItemDrops(0, 0, 37, 0, 13, true).Mul(ItemMultiplier);

    public ItemDrops? DropItems {
        get {
            if (GameManagement.instance.mode.DisallowCardItems()) return null;
            if (Captured == true) return DropCapture;
            else if (Cleared == true) return DropClear;
            else if (noHits) return DropNoHit;
            return null;
        }
    }

    public PhaseCompletion(PhaseProperties props, PhaseClearMethod clear, BehaviorEntity exec, CampaignSnapshot snap, float elapsed_ratio) {
        this.props = props;
        this.clear = clear;
        this.exec = exec;
        this.elapsed = elapsed_ratio;
        this.noHits = GameManagement.instance.HitsTaken == snap.hitsTaken;

    }
}
}