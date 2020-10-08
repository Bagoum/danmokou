using System;
using System.Linq;
using System.Threading;
using Core;
using JetBrains.Annotations;
using SM;
using UnityEngine;
using static Danmaku.Enums;

[Serializable]
public struct LocalizedString {
    public string en;
    public string jp;

    public LocalizedString(string en) {
        this.en = en;
        jp = null;
    }

    [CanBeNull]
    public string Value {
        get {
            switch (SaveData.s.Locale) {
                case Locale.EN: return en;
                case Locale.JP: return jp;
                default: return null;
            }
        }
    }

    public string ValueOrEn => Value ?? en;

    public static implicit operator string(LocalizedString ls) => ls.ValueOrEn;
}

namespace Danmaku {
//This struct is effectively readonly but these are json requirements.
public struct DifficultySettings {
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
    public float CustomValue => DifficultyForSlider(customValueSlider);
    public float customCounter;
    public int customValueSlider;
    public int numSuicideBullets;
    public float playerDamageMod;
    /// <summary>
    /// This only affects simple bullets, since lasers/pathers are often critical in pattern synchronization.
    /// </summary>
    public float bulletSpeedMod;
    public float bossHPMod;
    public readonly float FRAME_TIME_BULLET;
    public DifficultySettings(FixedDifficulty standard) : this((FixedDifficulty?)standard) { }

    public DifficultySettings(FixedDifficulty? standard, int slider=DEFAULT_SLIDER, int numSuicideBullets = 0,
        float playerDamageMod=1f, float bulletSpeedMod=1f, float bossHPMod=1f) {
        this.standard = standard;
        customValueSlider = slider;
        customCounter = Nearest(slider).Counter();
        this.numSuicideBullets = numSuicideBullets;
        this.playerDamageMod = playerDamageMod;
        this.bulletSpeedMod = bulletSpeedMod;
        this.FRAME_TIME_BULLET = ETime.FRAME_TIME * bulletSpeedMod;
        this.bossHPMod = bossHPMod;
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

    public float Value => standard?.Value() ?? CustomValue;
    public float Counter => standard?.Counter() ?? customCounter;

    public string Describe => standard?.Describe() ?? $"CUST:{customValueSlider:00}";
    /// <summary>
    /// For filenames
    /// </summary>
    public string DescribeSafe => standard?.Describe() ?? $"CUST{customValueSlider:00}";
    public string DescribePadR => Describe.PadRight(8);
}
public readonly struct SMRunner {
    [CanBeNull] public readonly StateMachine sm;
    public readonly ICancellee cT;
    public readonly bool cullOnFinish;
    [CanBeNull] private readonly GenCtx gcx;
    [CanBeNull] public GenCtx NewGCX => gcx?.Copy();

    public static SMRunner Null => new SMRunner(null, Cancellable.Null, false, null);
    public static SMRunner Cull(StateMachine sm, ICancellee cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, true, gcx);
    public static SMRunner Run(StateMachine sm, ICancellee cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, false, gcx);
    public static SMRunner RunNoCancel(StateMachine sm) => Run(sm, Cancellable.Null);
    public SMRunner(StateMachine sm, ICancellee cT, bool cullOnFinish, [CanBeNull] GenCtx gcx) {
        this.sm = sm;
        this.cT = cT;
        this.cullOnFinish = cullOnFinish;
        this.gcx = gcx;
    }
}

public readonly struct PhaseCompletion {
    public readonly PhaseProperties props;
    public readonly PhaseClearMethod clear;
    public readonly BehaviorEntity exec;
    public readonly bool noHits;
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

    private ItemDrops DropCapture => new ItemDrops(42, 0, 42, 0, 24, true).Mul(props.cardValueMult);
    //Final spells give no items if not captured, this is because some final spells have infinite timers
    private ItemDrops DropClear => new ItemDrops(
        props.phaseType == PhaseType.FINAL ? 0 : 29, 0, 13, 0, 20, true).Mul(props.cardValueMult);
    private ItemDrops DropNoHit => new ItemDrops(0, 0, 37, 0, 13, true).Mul(props.cardValueMult);

    public ItemDrops? DropItems {
        get {
            if (GameManagement.campaign.mode.DisallowItems()) return null;
            if (Captured == true) return DropCapture;
            else if (Cleared == true) return DropClear;
            else if (noHits) return DropNoHit;
            return null;
        }
    }

    public PhaseCompletion(PhaseProperties props, PhaseClearMethod clear, BehaviorEntity exec, in CampaignData cmpStart) {
        this.props = props;
        this.clear = clear;
        this.exec = exec;
        this.noHits = GameManagement.campaign.HitsTaken == cmpStart.HitsTaken;

    }
}
}