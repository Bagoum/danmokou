using System;
using System.Collections.Generic;
using System.Linq;
using DMK.Core;
using DMK.DMath;
using DMK.GameInstance;
using DMK.Player;
using DMK.Scriptables;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;
using InstanceLowRequest = DMK.Core.DU<DMK.GameInstance.CampaignRequest, DMK.GameInstance.BossPracticeRequest, 
    DMK.GameInstance.PhaseChallengeRequest, DMK.GameInstance.StagePracticeRequest>;
using InstanceLowRequestKey = DMK.Core.DU<string, ((string, int), int), ((((string, int), int), int), int), ((string, int), int)>;


namespace DMK.GameInstance {

//This uses boss key instead of boss index since phaseSM doesn't have trivial access to boss index
[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct CardHistory {
    public string campaign;
    public string boss;
    public int bossIndex;
    public int phase;
    public bool captured;
    [JsonIgnore] [ProtoIgnore]
    public ((string campaign, int boss), int phase) Key => ((campaign, bossIndex), phase);
}
/// <summary>
/// Records information about a game run-through that may or may not have been completed.
/// </summary>
[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class InstanceRecord {
    public SharedInstanceMetadata.Saveable SavedMetadata { get; set; }

    [JsonIgnore] [ProtoIgnore] 
    private SharedInstanceMetadata? _lazySharedInstanceMeta = null;
    [JsonIgnore] [ProtoIgnore]
    public SharedInstanceMetadata SharedInstanceMetadata => 
        _lazySharedInstanceMeta ??= new SharedInstanceMetadata(SavedMetadata);
    
    public InstanceMode Mode { get; set; }
    public (string campaign, 
        ((string campaign, int boss), int phase) boss, 
        ((((string campaign, int day), int boss), int phase), int challenge) challenge, 
        ((string campaign, int stage), int phase) stage, short type) 
        RequestKey { get; set; }

    public int Seed { get; set; }
    public string Uuid { get; set; }
    public string CustomName { get; set; }
    [JsonIgnore] [ProtoIgnore]
    public string CustomNameOrPartial => string.IsNullOrEmpty(CustomName) ?
        (Completed ? "" : "[PARTIAL]") :
        CustomName;

    public DateTime Date { get; set; }
    public long Score { get; set; }

    public Version EngineVersion { get; set; }
    public Version GameVersion { get; set; }
    public string GameIdentifier { get; set; }
    
    //
    public List<CardHistory> CardCaptures { get; set; } = new List<CardHistory>();
    public bool OneCreditClear { get; set; }
    public bool Completed { get; set; }
    
    public AyaPhoto[] Photos { get; set; } = new AyaPhoto[0];

    public string? Ending { get; set; } = null;
    
    //Miscellaneous stats
    public int HitsTaken { get; set; }
    public int TotalFrames { get; set; }
    public int MeterFrames { get; set; }

#pragma warning disable 8618
    public InstanceRecord() { } //JSON constructor
#pragma warning restore 8618
    public InstanceRecord(InstanceRequest req, InstanceData end, bool completed) {
        SavedMetadata = new SharedInstanceMetadata.Saveable(req.metadata);
        RequestKey = InstanceRequest.CampaignIdentifier(req.lowerRequest).Tuple;
        Mode = req.Mode;
        Seed = req.seed;
        Uuid = RNG.RandStringOffFrame();
        CustomName = "";
        Date = DateTime.Now;
        EngineVersion = GameManagement.EngineVersion;
        GameVersion = GameManagement.References.gameVersion;
        GameIdentifier = GameManagement.References.gameIdentifier;
        Completed = completed;

        Update(end);
    }

    /// <summary>
    /// A record may be created earlier than the actual completion of the game instance.
    /// In such cases, you can call this method to make sure the correct values are set when the instance finishes.
    /// </summary>
    public void Update(InstanceData end) {
        Score = end.Score;
        CardCaptures = end.CardCaptures.ToList();
        OneCreditClear = !end.Continued;
        HitsTaken = end.HitsTaken;
        TotalFrames = end.TotalFrames;
        MeterFrames = end.MeterFrames;
    }

    [JsonIgnore] [ProtoIgnore]
    public InstanceLowRequestKey ReconstructedRequestKey =>
        new InstanceLowRequestKey(RequestKey.Item5, RequestKey.Item1, RequestKey.Item2, RequestKey.Item3, RequestKey.Item4);
    
    [JsonIgnore] [ProtoIgnore]
    public InstanceLowRequest ReconstructedRequest =>
        ReconstructedRequestKey.Resolve(
            c => new InstanceLowRequest(CampaignRequest.Reconstruct(c)),
            b => new InstanceLowRequest(BossPracticeRequest.Reconstruct(b)),
            c => new InstanceLowRequest(PhaseChallengeRequest.Reconstruct(c)),
            s => new InstanceLowRequest(StagePracticeRequest.Reconstruct(s))
        );
    public void AssignName(string newName) => CustomName = newName.Substring(0, Math.Min(newName.Length, 10));

    public const int BossPracticeNameLength = 11;
    [JsonIgnore] [ProtoIgnore]
    private string RequestDescription => ReconstructedRequest.Resolve(
        //4+8
        c => $"All:{c.campaign.campaign.shortTitle.PadRight(8)}",
        //3+9
        b => $"p{b.phase.IndexInParentPhases}:{b.boss.boss.ReplayName.ValueOrEn.PadRight(9)}",
        //5+9 -- note that challenges records do not show next to standard records, so misalignment is OK
        c => $"p{c.phase.phase.IndexInParentPhases}-{c.ChallengeIdx}:{c.Boss.ReplayName.ValueOrEn.PadRight(9)}",
        //8+3
        s => $"s{s.stage.stageIndex}:{s.stage.campaign.campaign.shortTitle.PadRight(8)}"
    ).PadRight(12);
    
    public LocalizedString AsDisplay(bool showScore, bool showRequest, bool defaultName=false, bool showTime=true) {
        var team = SharedInstanceMetadata.team;
        var p = team.players.TryN(0)?.player;
        var s = team.players.TryN(0)?.shot;
        var pstr = ShotConfig.PlayerShotDescription(p, s).PadRight(10);
        var score = showScore ? $"{Score} ".PadLeft(10, '0') : "";
        var name = (string.IsNullOrEmpty(CustomNameOrPartial) && defaultName) ? "[NAME]" : CustomNameOrPartial;
        var req = showRequest ? $"{RequestDescription} " : "";
        var date = showTime ? Date.SimpleTime() : Date.SimpleDate();
        return new LocalizedString($"{name.PadRight(12)} {score} {pstr} {req}{SavedMetadata.difficulty.DescribePadR()} {date}");
    }
}


}