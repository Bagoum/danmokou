using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Achievements;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.GameInstance;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Danmaku {

/// <summary>
/// Game definition for danmaku games (base interface for campaign games and scene games).
/// </summary>
public interface IDanmakuGameDef : IGameDef {
    InstanceFeatures MakeFeatures(DifficultySettings difficulty, long? highScore);
    SceneConfig ReplaySaveMenu { get; }
    FieldBounds Bounds { get; }
    
    public static IEnumerable<ShipConfig> CampaignShots(CampaignConfig? c) =>
        c == null ? new ShipConfig[0] : c.players;

    public static IEnumerable<ShipConfig> CampaignShots(DayCampaignConfig? c) =>
        c == null ? new ShipConfig[0] : c.players;

    public IEnumerable<ShipConfig> AllShips { get; }
    public IEnumerable<ShotConfig> AllShots => AllShips.SelectMany(x => x.shots2.Select(s => s.shot));
    public IEnumerable<IAbilityCfg> AllSupportAbilities => 
        AllShips.SelectMany(x => x.supports.Select(s => s.ability));

    public ShipConfig FindPlayer(string key) => AllShips.First(p => p.key == key);
    public ShotConfig FindShot(string key) => AllShots.First(s => s.key == key);

    public IAbilityCfg? FindSupportAbility(string key) =>
        AllSupportAbilities.FirstOrDefault(x => x.Key == key);
}
/// <summary>
/// Game definition for campaign-based danmaku games.
/// </summary>
public interface ICampaignDanmakuGameDef : IDanmakuGameDef {
    SceneConfig Endcard { get; }
    CampaignConfig Campaign { get; }
    CampaignConfig? ExCampaign { get; }
    public IEnumerable<CampaignConfig> Campaigns => new[] {Campaign, ExCampaign}.FilterNone();
}

/// <summary>
/// Game definition for scene-based danmaku games.
/// </summary>
public interface ISceneDanmakuGameDef : IDanmakuGameDef {
    DayCampaignConfig DayCampaign { get; }
}

public abstract class DanmakuGameDef : GameDef, IDanmakuGameDef {
    public SceneConfig m_replaySaveMenu = null!;
    public FieldBounds m_bounds = new() {
        left = -3.6f,
        right = 3.6f,
        top = 4.1f,
        bot = -4.5f,
        center = new Vector2(-1.9f, 0),
    };
    public abstract InstanceFeatures MakeFeatures(DifficultySettings difficulty, long? highScore);
    public SceneConfig ReplaySaveMenu => m_replaySaveMenu;
    public FieldBounds Bounds => m_bounds;
    public abstract IEnumerable<ShipConfig> AllShips { get; }
}

public abstract class CampaignDanmakuGameDef : DanmakuGameDef, ICampaignDanmakuGameDef {
    public SceneConfig m_endcard = null!;
    public CampaignConfig m_campaign = null!;
    public CampaignConfig? m_extraCampaign;
    public SceneConfig Endcard => m_endcard;
    public CampaignConfig Campaign => m_campaign;
    public CampaignConfig? ExCampaign => m_extraCampaign;
    public override IEnumerable<ShipConfig> AllShips => 
        IDanmakuGameDef.CampaignShots(Campaign).Concat(
            IDanmakuGameDef.CampaignShots(ExCampaign));
}

public abstract class SceneDanmakuGameDef : DanmakuGameDef, ISceneDanmakuGameDef {
    public DayCampaignConfig m_dayCampaign = null!;
    public DayCampaignConfig DayCampaign => m_dayCampaign;
    public override IEnumerable<ShipConfig> AllShips => IDanmakuGameDef.CampaignShots(DayCampaign);
}

}