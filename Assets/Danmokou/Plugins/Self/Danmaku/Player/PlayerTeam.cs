using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {
public struct PlayerTeam {
    public readonly (PlayerConfig player, ShotConfig shot)[] players;
    public int SelectedIndex { get; set; }
    public Enums.Subshot Subshot { get; set; }
    [CanBeNull] public PlayerConfig Player => players.TryN(SelectedIndex)?.player;
    [CanBeNull] public ShotConfig Shot => players.TryN(SelectedIndex)?.shot;

    public string Describe => string.Join("-", players.Select(p => $"{p.player.key}_{p.shot.key}"));

    public static readonly PlayerTeam Empty = new PlayerTeam(0, Enums.Subshot.TYPE_D);

    public PlayerTeam(int which, Enums.Subshot sub, params (PlayerConfig, ShotConfig)[] players) {
        this.players = players;
        SelectedIndex = which;
        Subshot = sub;
    }
    public PlayerTeam(Saveable saved) : this(saved.selectedIndex, saved.subshot, saved.players.Select(p => (
        GameManagement.References.FindPlayer(p.playerKey), 
        GameManagement.References.FindShot(p.shotKey))).ToArray()) { }

    [Serializable]
    public struct Saveable {
        public (string playerKey, string shotKey)[] players { get; set; }
        public int selectedIndex { get; set; }
        public Enums.Subshot subshot { get; set; }

        public Saveable(PlayerTeam team) {
            players = team.players.Select(p => (p.player.key, p.shot.key)).ToArray();
            selectedIndex = team.SelectedIndex;
            subshot = team.Subshot;
        }
    }
}
}