using System;
using System.Linq;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Player;
using static Danmokou.Services.GameManagement;

namespace Danmokou.Achievements {

public class UsedMeterReq : Requirement {
    public UsedMeterReq() {
        Listen(PlayerController.MeterIsActive);
    }

    public override State EvalState() => (Instance.MeterFrames > 30).ToACVState();
}

/// <summary>
/// The phase requirement only concerns the number of card phases.
/// Eg. A boss may have 5 total phases, 4 typed (nontrivial) phases, 2 card phases (and 2 dialogue phases).
/// </summary>
public class StarsRequirement : Requirement {
    private readonly string campaign;
    private readonly string boss;
    private (string, string) BossKey => (campaign, boss);
    //stars -> phases -> bool
    private readonly Func<int, int, bool> predicate;
    
    
    public StarsRequirement(string campaign, string boss, Func<int, int, bool> predicate) {
        this.campaign = campaign;
        this.boss = boss;
        this.predicate = predicate;
        Listen(EvInstance, i => i.CardHistoryUpdated);
    }
    
    public StarsRequirement(string campaign, string boss, int phases) : this(campaign, boss,
        (s, p) => s >= phases * PhaseCompletion.MaxCaptureStars && p >= phases) { }
    
    public override State EvalState() =>
        (GameManagement.Instance.IsAtleastNormalCampaign &&
         GameManagement.Instance.campaignKey == campaign &&
        predicate(GameManagement.Instance.CardHistory.Stars(boss),
            GameManagement.Instance.CardHistory.NCards(boss))).ToACVState();
}

public class CardCompletionRequirement : Requirement {
    private readonly string campaign;
    private readonly string boss;
    private readonly int phaseIndex;
    private readonly Func<CardRecord, bool> predicate;
    
    public CardCompletionRequirement(string campaign, string boss, int phaseIndex, Func<CardRecord, bool> predicate) {
        this.campaign = campaign;
        this.boss = boss;
        this.phaseIndex = phaseIndex;
        this.predicate = predicate;
        Listen(EvInstance, i => i.CardHistoryUpdated);
    }

    public override State EvalState() {
        if (!GameManagement.Instance.IsAtleastNormalCampaign || Instance.campaignKey != campaign) 
            return State.InProgress;
        var rec = GameManagement.Instance.CardHistory.RecordForBoss(boss);
        if (rec == null || rec.All(r => r.phase != phaseIndex)) 
            return State.InProgress;
        return predicate(rec.First(r => r.phase == phaseIndex)).ToACVState();
    }
}


public class ScoreReq : Requirement {
    private readonly long score;

    public ScoreReq(long score) {
        this.score = score;
        Listen(EvInstance, i => i.Score);
    }

    public override State EvalState() => (Instance.Score >= score).ToACVState();
}
public class CampaignScoreReq : Requirement {
    private readonly long score;

    public CampaignScoreReq(long score) {
        this.score = score;
        Listen(EvInstance, i => i.Score);
    }

    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.Score >= score).ToACVState();
}

public class CampaignGrazeReq : Requirement {
    private readonly int graze;

    public CampaignGrazeReq(int graze) {
        this.graze = graze;
        Listen(EvInstance, i => i.Graze);
    }
    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.Graze >= graze).ToACVState();
}

public class CampaignPIVReq : Requirement {
    private readonly double piv;

    public CampaignPIVReq(double piv) {
        this.piv = piv;
        Listen(EvInstance, i => i.PIV);
    }
    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.PIV >= piv).ToACVState();
}


}