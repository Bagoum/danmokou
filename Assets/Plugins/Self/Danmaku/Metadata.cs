using System.Threading;
using Core;
using JetBrains.Annotations;
using SM;
using static Danmaku.Enums;

namespace Danmaku {

public readonly struct SMRunner {
    [CanBeNull] public readonly StateMachine sm;
    public readonly CancellationToken cT;
    public readonly bool cullOnFinish;
    [CanBeNull] private readonly GenCtx gcx;
    [CanBeNull] public GenCtx NewGCX => gcx?.Copy();

    public static SMRunner Null => new SMRunner(null, CancellationToken.None, false, null);
    public static SMRunner Cull(StateMachine sm, CancellationToken cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, true, gcx);
    public static SMRunner Run(StateMachine sm, CancellationToken cT, [CanBeNull] GenCtx gcx=null) => new SMRunner(sm, cT, false, gcx);
    public static SMRunner RunNoCancel(StateMachine sm) => Run(sm, CancellationToken.None);
    public SMRunner(StateMachine sm, CancellationToken cT, bool cullOnFinish, [CanBeNull] GenCtx gcx) {
        this.sm = sm;
        this.cT = cT;
        this.cullOnFinish = cullOnFinish;
        this.gcx = gcx;
    }
}

public enum PhaseClearMethod {
    HP,
    TIMEOUT,
    CANCELLED
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
        (bool?) ((clear == PhaseClearMethod.HP) ||
                 //For timeouts, clearing requires no-hit
                 (props.phaseType == PhaseType.TIMEOUT && clear == PhaseClearMethod.TIMEOUT && noHits))
        : null;

    /// <summary>
    /// True if the phase is a card-type phase and it was not externally cancelled.
    /// </summary>
    public bool StandardCardFinish => (props.phaseType?.IsCard() ?? false) && clear != PhaseClearMethod.CANCELLED;

    private ItemDrops DropCapture => new ItemDrops(42, 0, 42, true).Mul(props.cardValueMult);
    //Final spells give no items if not captured, this is because some final spells have infinite timers
    private ItemDrops DropClear => new ItemDrops(
        props.phaseType == PhaseType.FINAL ? 0 : 29, 0, 13, true).Mul(props.cardValueMult);
    private ItemDrops DropNoHit => new ItemDrops(0, 0, 37, true).Mul(props.cardValueMult);

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