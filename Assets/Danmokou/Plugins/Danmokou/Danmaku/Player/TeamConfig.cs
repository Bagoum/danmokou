using System;
using System.Linq;
using Danmokou.Core;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using ProtoBuf;

namespace Danmokou.Player {
public class ActiveTeamConfig {
    private readonly TeamConfig team;
    //Index/subshot in team are initial values, which may be overriden here by eg. powerups.
    public int SelectedIndex { get; set; }
    public Subshot Subshot { get; set; }
    public SupportAbility Support { get; }
    public (ShipConfig ship, ShotConfig shot)[] Ships => team.ships;
    public ShipConfig Ship => team.ships[SelectedIndex].ship;
    public ShotConfig Shot => team.ships[SelectedIndex].shot;
    public bool HasMultishot => team.HasMultishot;


    public ActiveTeamConfig(TeamConfig team) {
        this.team = team;
        SelectedIndex = team.selectedIndex;
        Subshot = team.subshot;
        Support = team.supportAbility?.Value ?? new NullAbility();
    }
}
public readonly struct TeamConfig {
    public readonly (ShipConfig ship, ShotConfig shot)[] ships;
    public readonly int selectedIndex;
    public readonly ISupportAbilityConfig? supportAbility;
    public readonly Subshot subshot;
    public bool HasMultishot => ships.Any(s => s.shot.isMultiShot);
    
    public TeamConfig(int which, Subshot sub, ISupportAbilityConfig? support, params (ShipConfig, ShotConfig)[] ships) {
        this.ships = ships;
        this.supportAbility = support;
        selectedIndex = which;
        subshot = sub;
    }
    public TeamConfig(Saveable saved) : this(saved.SelectedIndex, saved.Subshot, 
        GameManagement.References.FindSupportAbility(saved.SupportAbilityKey), saved.Players.Select(p => (
        GameManagement.References.FindPlayer(p.playerKey), 
        GameManagement.References.FindShot(p.shotKey))).ToArray()) { }

    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Saveable {
        public (string playerKey, string shotKey)[] Players { get; set; }
        public int SelectedIndex { get; set; }
        public string SupportAbilityKey { get; set; }
        public Subshot Subshot { get; set; }

        public Saveable(TeamConfig team) {
            Players = team.ships.Select(p => (p.ship.key, p.shot.key)).ToArray();
            SelectedIndex = team.selectedIndex;
            SupportAbilityKey = team.supportAbility?.Key ?? "";
            Subshot = team.subshot;
        }
    }
}
}