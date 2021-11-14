﻿using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using Newtonsoft.Json;
using ProtoBuf;


namespace Danmokou.GameInstance {

[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CardHistory {
    /// <summary>
    /// A dictionary mapping a boss key to the performance information for each of its cards.
    /// </summary>
    public Dictionary<string, List<CardRecord>> record = new();

    public CardHistory() { }
    public CardHistory(CardHistory copy) {
        foreach (var key in copy.record.Keys)
            record[key] = copy.record[key].ToList();
    }

    public void Add(CardRecord c) => record.AddToList(c.boss, c);

    public void Clear() {
        record.Clear();
    }

    public List<CardRecord>? RecordForBoss(string boss) =>
        record.TryGetValue(boss, out var l) ? l : null;

    public int Stars(string boss) => RecordForBoss(boss)?.Sum(x => x.stars) ?? 0;
    public int NCards(string boss) => RecordForBoss(boss)?.Count ?? 0;
}

public class CardHistoryStats {
    public int TotalCards { get; }
    public int[] CardsPerStarCount { get; } = new int[PhaseCompletion.MaxCaptureStars + 1];
    public CardHistoryStats(CardHistory ch) {
        foreach (var (_, crs) in ch.record) {
            foreach (var cr in crs) {
                ++TotalCards;
                ++CardsPerStarCount[cr.stars];
            }
        }
    }
}

//This uses boss key instead of boss index since phaseSM doesn't have trivial access to boss index
[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public struct CardRecord {
    public string campaign;
    //We use boss identifier instead of boss index because running code does not necessarily
    // know the boss index.
    public string boss;
    public int phase;
    public int stars;
    public PhaseClearMethod? method;
    public int hits;
    [JsonIgnore] [ProtoIgnore] 
    public bool NoHits => hits == 0;
    [JsonIgnore] [ProtoIgnore] 
    public bool Captured => stars >= 2;
    [JsonIgnore] [ProtoIgnore]
    public BossPracticeRequestKey Key => new() {
        Campaign = campaign,
        Boss = boss,
        PhaseIndex = phase
    };
}
/// <summary>
/// Records information about a game run-through that may or may not have been completed.
/// </summary>
[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class InstanceRecord {
    public SharedInstanceMetadata.Saveable SavedMetadata { get; init; }

    [JsonIgnore] [ProtoIgnore] 
    private SharedInstanceMetadata? _lazySharedInstanceMeta = null;
    [JsonIgnore] [ProtoIgnore]
    public SharedInstanceMetadata SharedInstanceMetadata => 
        _lazySharedInstanceMeta ??= new SharedInstanceMetadata(SavedMetadata);

    [JsonIgnore] [ProtoIgnore] public DifficultySettings Difficulty => SavedMetadata.difficulty;
    public InstanceMode Mode { get; init; }
    [JsonIgnore] [ProtoIgnore] public bool IsCampaign => Mode == InstanceMode.CAMPAIGN;
    [JsonIgnore] [ProtoIgnore] public bool IsAtleastNormalCampaign => IsCampaign && Difficulty.standard >= FixedDifficulty.Normal;
    
    public string Campaign { get; init; }
    public LowInstanceRequestKey RequestKey { get; init; }

    public int Seed { get; init; }
    public string Uuid { get; init; }
    public string CustomName { get; set; }
    [JsonIgnore] [ProtoIgnore]
    public string CustomNameOrPartial => string.IsNullOrEmpty(CustomName) ?
        (Completed ? "" : "[PARTIAL]") :
        CustomName;

    public DateTime Date { get; init; }
    public long Score { get; set; }

    public Version EngineVersion { get; init; }
    public Version GameVersion { get; init; }
    public string GameIdentifier { get; init; }

    public CardHistory CardHistory { get; set; } = new();
    public int ContinuesUsed { get; set; }
    [JsonIgnore] [ProtoIgnore] public bool OneCreditClear => Completed && ContinuesUsed == 0;
    public bool Completed { get; init; }
    
    public AyaPhoto[] Photos { get; init; } = new AyaPhoto[0];

    public string? Ending { get; init; } = null;
    
    //Miscellaneous stats
    public int HitsTaken { get; set; }
    public int TotalFrames { get; set; }
    public int MeterFrames { get; set; }
    public int BombsUsed { get; set; }
    public int SubshotSwitches { get; set; }
    public int OneUpItemsCollected { get; set; }

#pragma warning disable 8618
    /// <summary>
    /// JSON constructor, do not use
    /// </summary>
    [Obsolete]
    public InstanceRecord() { }
#pragma warning restore 8618
    public InstanceRecord(InstanceRequest req, InstanceData end, bool completed) {
        SavedMetadata = new SharedInstanceMetadata.Saveable(req.metadata);
        Campaign = end.campaignKey;
        RequestKey = req.lowerRequest.Key;
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
        CardHistory = new CardHistory(end.CardHistory);
        ContinuesUsed = end.ContinuesUsed;
        HitsTaken = end.HitsTaken;
        TotalFrames = end.TotalFrames;
        MeterFrames = end.MeterFrames;
        BombsUsed = end.BombsUsed;
        SubshotSwitches = end.SubshotSwitches;
        OneUpItemsCollected = end.OneUpItemsCollected;
    }

    [JsonIgnore] [ProtoIgnore] public ILowInstanceRequest ReconstructedRequest => RequestKey.Reconstruct();
    public void AssignName(string newName) => CustomName = newName[..Math.Min(newName.Length, 10)];

    public const int BossPracticeNameLength = 11;
    [JsonIgnore]
    [ProtoIgnore]
    private string RequestDescription => (ReconstructedRequest switch {
        //4+8
        CampaignRequest c => $"All:{c.campaign.campaign.shortTitle}",
        //4+10
        BossPracticeRequest b => $"p{b.phase.NontrivialPhaseIndex.PadLZero(2)}:{b.boss.boss.ReplayName.Value}",
        //5+9 -- note that challenges records do not show next to standard records, so misalignment is OK
        PhaseChallengeRequest c =>
            $"p{c.phase.phase.NontrivialPhaseIndex}-{c.ChallengeIdx}:{c.Boss.ReplayName.Value,-9}",
        //3+10
        StagePracticeRequest s => $"s{s.stage.stageIndex + 1}:{s.stage.campaign.campaign.shortTitle}",
        _ => throw new Exception($"No description handling for request type {ReconstructedRequest.GetType()}")
    }).PadRight(14);
    
    public LString AsDisplay(bool showScore, bool showRequest, bool defaultName=false, bool showTime=true) {
        var team = SharedInstanceMetadata.team;
        var p = team.ships.TryN(0)?.ship;
        var s = team.ships.TryN(0)?.shot;
        var pstr =  ShotConfig.PlayerShotDescription(p, s).Value.PadRight(11);
        var score = showScore ? $"{Score} ".PadLeft(11, '0') : "";
        var name = (string.IsNullOrEmpty(CustomNameOrPartial) && defaultName) ? "[NAME]" : CustomNameOrPartial;
        var req = showRequest ? $"{RequestDescription} " : "";
        var date = showTime ? Date.SimpleTime() : Date.SimpleDate();
        return new LString($"{name,-16} {score} {pstr} {req}{SavedMetadata.difficulty.DescribePadR()}  {date}");
    }
}


}