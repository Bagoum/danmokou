using System;
using System.Linq;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using ProtoBuf;

namespace Danmokou.Player {
public class ActiveTeamConfig {
    private readonly TeamConfig team;
    private int _selIndex;
    //Index/subshot in team are initial values, which may be overriden here by eg. powerups.
    public int SelectedIndex { get => _selIndex;
        set {
            _selIndex = value;
            Support = team.ships[_selIndex].support?.Value;
        } }
    public Subshot Subshot { get; set; }
    public (ShipConfig ship, ShotConfig shot, IAbilityCfg? support)[] Ships => team.ships;
    public ShipConfig Ship => team.ships[SelectedIndex].ship;
    public ShotConfig Shot => team.ships[SelectedIndex].shot;
    public Ability? Support { get; private set; }
    public bool HasMultishot => team.HasMultishot;


    public ActiveTeamConfig(TeamConfig team) {
        this.team = team;
        SelectedIndex = team.selectedIndex;
        Subshot = team.subshot;
    }
}
public readonly struct TeamConfig {
    public readonly (ShipConfig ship, ShotConfig shot, IAbilityCfg? support)[] ships;
    public readonly int selectedIndex;
    public readonly Subshot subshot;
    public bool HasMultishot => ships.Any(s => s.shot.isMultiShot);
    
    public TeamConfig(int which, Subshot sub, params (ShipConfig, ShotConfig, IAbilityCfg?)[] ships) {
        this.ships = ships;
        selectedIndex = which;
        subshot = sub;
    }
    public TeamConfig(Saveable saved, IDanmakuGameDef game) : this(saved.SelectedIndex, saved.Subshot, 
        saved.Players.Select(p => (
            game.FindPlayer(p.playerKey), 
            game.FindShot(p.shotKey), game.FindSupportAbility(p.abilityKey))).ToArray()) { }

    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Saveable {
        public (string playerKey, string shotKey, string abilityKey)[] Players { get; set; }
        public int SelectedIndex { get; set; }
        public Subshot Subshot { get; set; }

        public Saveable(TeamConfig team) {
            Players = team.ships.Select(p => (p.ship.key, p.shot.key, p.support?.Key ?? "")).ToArray();
            SelectedIndex = team.selectedIndex;
            Subshot = team.subshot;
        }
    }
}
}