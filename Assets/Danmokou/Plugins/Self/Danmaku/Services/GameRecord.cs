using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using GameLowRequest = DU<Danmaku.CampaignRequest, Danmaku.BossPracticeRequest, 
    PhaseChallengeRequest, Danmaku.StagePracticeRequest>;
using GameLowRequestKey = DU<string, ((string, int), int), ((((string, int), int), int), int), ((string, int), int)>;


namespace Danmaku {

//This uses boss key instead of boss index since phaseSM doesn't have trivial access to boss index
[Serializable]
public struct CardHistory {
    public string campaign;
    public string boss;
    public int phase;
    public bool captured;
    [JsonIgnore]
    public (string, string, int) Key => (campaign, boss, phase);
}
/// <summary>
/// Records information about a game run-through that may or may not have been completed.
/// </summary>
public class GameRecord {
    public GameMetadata.Saveable SavedMetadata { get; set; }
    [JsonIgnore]
    public GameMetadata GameMetadata => new GameMetadata(SavedMetadata);
    
    public Enums.CampaignMode Mode { get; set; }
    public (string campaign, 
        ((string campaign, int boss), int phase) boss, 
        ((((string campaign, int day), int boss), int phase), int challenge) challenge, 
        ((string campaign, int stage), int phase) stage, short type) 
        GameKey { get; set; }

    public int Seed { get; set; }
    public string Uuid { get; set; }
    public string CustomName { get; set; }
    [JsonIgnore] 
    public string CustomNameOrPartial => string.IsNullOrEmpty(CustomName) ?
        (Completed ? "" : "[PARTIAL]") :
        CustomName;

    public DateTime Date { get; set; }
    public long Score { get; set; }

    public Version EngineVersion { get; set; }
    public Version TitleVersion { get; set; }
    public string TitleIdentifier { get; set; }
    
    //
    public List<CardHistory> CardCaptures { get; set; } = new List<CardHistory>();
    public bool OneCreditClear { get; set; }
    public bool Completed { get; set; }
    public GameRecord() { } //JSON constructor
    public GameRecord(GameRequest req, CampaignData end, bool completed) {
        SavedMetadata = new GameMetadata.Saveable(req.metadata);
        GameKey = GameRequest.CampaignIdentifier(req.lowerRequest).Tuple;
        Mode = req.Mode;
        Seed = req.seed;
        Uuid = RNG.RandStringOffFrame();
        CustomName = "";
        Date = DateTime.Now;
        Score = end.Score;
        EngineVersion = GameManagement.EngineVersion;
        TitleVersion = GameManagement.References.gameVersion;
        TitleIdentifier = GameManagement.References.gameIdentifier;
        Completed = completed;
        CardCaptures = end.CardCaptures.ToList();
        OneCreditClear = !end.Continued;
    }

    [JsonIgnore]
    public GameLowRequest ReconstructedRequest {
        get {
            var keydu = new GameLowRequestKey(GameKey.Item5, GameKey.Item1, GameKey.Item2, GameKey.Item3, GameKey.Item4);
            return keydu.Resolve(
                c => new GameLowRequest(CampaignRequest.Reconstruct(c)),
                b => new GameLowRequest(BossPracticeRequest.Reconstruct(b)),
                c => new GameLowRequest(PhaseChallengeRequest.Reconstruct(c)),
                s => new GameLowRequest(StagePracticeRequest.Reconstruct(s))
            );
        }
    }
    public void AssignName(string newName) => CustomName = newName.Substring(0, Math.Min(newName.Length, 10));

    [JsonIgnore]
    private string RequestDescription => ReconstructedRequest.Resolve(
        c => $"{c.campaign.campaign.shortTitle.PadRight(10)} All",
        b => $"{b.boss.boss.ReplayName.PadRight(10)} p{b.phase.IndexInParentPhases}",
        c => $"{c.Boss.ReplayName.PadRight(10)} p{c.phase.phase.IndexInParentPhases}-{c.ChallengeIdx}",
        s => $"{s.stage.campaign.campaign.shortTitle.PadRight(10)} s{s.stage.stageIndex}"
    );
    
    public string AsDisplay(bool showScore, bool showRequest, bool defaultName=false) {
        var team = GameMetadata.team;
        var p = team.players.TryN(0)?.player;
        var s = team.players.TryN(0)?.shot;
        var playerDesc = (p == null) ? "???" : p.shortTitle;
        var shotDesc = "?";
        if (p != null && s != null) {
            var shotInd = p.shots.IndexOf(s);
            shotDesc = (shotInd > -1) ? $"{shotInd.ToABC()}" : "?";
        }
        var pstr = $"{playerDesc}-{shotDesc}".PadRight(10);
        var score = showScore ? $"{Score} ".PadLeft(10, '0') : "";
        var name = (string.IsNullOrEmpty(CustomNameOrPartial) && defaultName) ? "[NAME HERE]" : CustomNameOrPartial;
        var req = showRequest ? $"{RequestDescription.PadRight(16)} " : "";
        return $"{name.PadRight(12)} {score} {pstr} {req}{SavedMetadata.difficulty.DescribePadR()} {Date.SimpleTime()}";
    }
}


}