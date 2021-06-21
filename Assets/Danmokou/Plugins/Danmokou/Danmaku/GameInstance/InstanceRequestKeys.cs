using System.Linq;
using Danmokou.SM;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Danmokou.GameInstance {
public interface ILowInstanceRequestKey {
    public string Campaign { get; }
    ILowInstanceRequest Reconstruct();
}

//record types when...
public class CampaignRequestKey : ILowInstanceRequestKey {
    public string Campaign { get; set; } = "";

    public ILowInstanceRequest Reconstruct() =>
        new CampaignRequest(SMAnalysis.AnalyzedCampaign.Reconstruct(Campaign));

    public override bool Equals(object obj) => obj is CampaignRequestKey cr && Campaign == cr.Campaign;

    public override int GetHashCode() => Campaign.GetHashCode();
}
public class BossPracticeRequestKey : ILowInstanceRequestKey {
    public string Campaign { get; set; } = "";
    public string Boss { get; set; } = "";
    public int PhaseIndex { get; set; }
    
    
    public ILowInstanceRequest Reconstruct() {
        var boss = SMAnalysis.AnalyzedBoss.Reconstruct(Campaign, Boss);
        return new BossPracticeRequest(boss, boss.Phases.First(p => p.index == PhaseIndex));
    }
    
    private (string, string, int) Tuple => (Campaign, Boss, PhaseIndex);
    
    public override bool Equals(object obj) => obj is BossPracticeRequestKey cr && Tuple == cr.Tuple;

    public override int GetHashCode() => Tuple.GetHashCode();
}

public class StagePracticeRequestKey : ILowInstanceRequestKey {
    public string Campaign { get; set; } = "";
    public int StageIndex { get; set; }
    public int PhaseIndex { get; set; }
    
    public ILowInstanceRequest Reconstruct() =>
        new StagePracticeRequest(SMAnalysis.AnalyzedStage.Reconstruct(Campaign, StageIndex), PhaseIndex);
    
    private (string, int, int) Tuple => (Campaign, StageIndex, PhaseIndex);
    
    public override bool Equals(object obj) => obj is StagePracticeRequestKey cr && Tuple == cr.Tuple;

    public override int GetHashCode() => Tuple.GetHashCode();
}

public class PhaseChallengeRequestKey : ILowInstanceRequestKey {
    public string Campaign { get; set; } = "";
    public int DayIndex { get; set; }
    public string Boss { get; set; } = "";
    public int PhaseIndex { get; set; }
    public int ChallengeIndex { get; set; }

    public ILowInstanceRequest Reconstruct() => 
        new PhaseChallengeRequest(SMAnalysis.DayPhase.Reconstruct(Campaign, DayIndex, Boss, PhaseIndex), ChallengeIndex);

    
    private (string, int, string, int, int) Tuple => (Campaign, DayIndex, Boss, PhaseIndex, ChallengeIndex);
    
    public override bool Equals(object obj) => obj is PhaseChallengeRequestKey cr && Tuple == cr.Tuple;

    public override int GetHashCode() => Tuple.GetHashCode();
}
}