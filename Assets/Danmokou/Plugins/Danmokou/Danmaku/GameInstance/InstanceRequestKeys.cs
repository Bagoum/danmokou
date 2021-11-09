using System.Linq;
using Danmokou.SM;
using ProtoBuf;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Danmokou.GameInstance {
[ProtoInclude(101, typeof(CampaignRequestKey))]
[ProtoInclude(102, typeof(BossPracticeRequestKey))]
[ProtoInclude(103, typeof(StagePracticeRequestKey))]
[ProtoInclude(104, typeof(StagePracticeRequestKey))]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public abstract record LowInstanceRequestKey {
    public string Campaign { get; init; } = "";
    public abstract ILowInstanceRequest Reconstruct();
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record CampaignRequestKey : LowInstanceRequestKey {
    public override ILowInstanceRequest Reconstruct() =>
        new CampaignRequest(SMAnalysis.AnalyzedCampaign.Reconstruct(Campaign));
}
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record BossPracticeRequestKey: LowInstanceRequestKey {
    public string Boss { get; init; } = "";
    public int PhaseIndex { get; init; }
    public override ILowInstanceRequest Reconstruct() {
        var boss = SMAnalysis.AnalyzedBoss.Reconstruct(Campaign, Boss);
        return new BossPracticeRequest(boss, boss.Phases.First(p => p.index == PhaseIndex));
    }
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record StagePracticeRequestKey: LowInstanceRequestKey {
    public int StageIndex { get; init; }
    public int PhaseIndex { get; init; }
    public override ILowInstanceRequest Reconstruct() =>
        new StagePracticeRequest(SMAnalysis.AnalyzedStage.Reconstruct(Campaign, StageIndex), PhaseIndex);
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record PhaseChallengeRequestKey : LowInstanceRequestKey {
    public int DayIndex { get; init; }
    public string Boss { get; init; } = "";
    public int PhaseIndex { get; init; }
    public int ChallengeIndex { get; init; }

    public override ILowInstanceRequest Reconstruct() => 
        new PhaseChallengeRequest(SMAnalysis.DayPhase.Reconstruct(Campaign, DayIndex, Boss, PhaseIndex), ChallengeIndex);
}
}