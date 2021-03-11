using System;
using System.Linq;
using DMK.Core;
using DMK.Danmaku;
using DMK.GameInstance;
using DMK.Player;
using static DMK.Core.GameManagement;

namespace DMK.Achievements {

public class UsedMeterReq : Requirement {
    public UsedMeterReq() {
        Listen(PlayerInput.MeterIsActive);
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
        Listen(InstanceData.CardHistoryUpdated);
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
        Listen(InstanceData.CardHistoryUpdated);
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

public class CampaignScoreReq : Requirement {
    private readonly long score;

    public CampaignScoreReq(long score) {
        this.score = score;
        Listen(InstanceData.CampaignDataUpdated);
    }

    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.Score >= score).ToACVState();
}

public class CampaignGrazeReq : Requirement {
    private readonly int graze;

    public CampaignGrazeReq(int graze) {
        this.graze = graze;
        Listen(InstanceData.CampaignDataUpdated);
    }
    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.Graze >= graze).ToACVState();
}

public class CampaignPIVReq : Requirement {
    private readonly double piv;

    public CampaignPIVReq(double piv) {
        this.piv = piv;
        Listen(InstanceData.CampaignDataUpdated);
    }
    public override State EvalState() => (Instance.IsAtleastNormalCampaign && Instance.PIV >= piv).ToACVState();
}


}