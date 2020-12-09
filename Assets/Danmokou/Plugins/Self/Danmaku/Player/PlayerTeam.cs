using System;
using System.Linq;
using DMK.Core;
using DMK.Scriptables;
using JetBrains.Annotations;
using ProtoBuf;

namespace DMK.Player {
public struct PlayerTeam {
    public readonly (PlayerConfig player, ShotConfig shot)[] players;
    public int SelectedIndex { get; set; }
    public Subshot Subshot { get; set; }
    [CanBeNull] public PlayerConfig Player => players.TryN(SelectedIndex)?.player;
    [CanBeNull] public ShotConfig Shot => players.TryN(SelectedIndex)?.shot;

    public string Describe => string.Join("-", players.Select(p => $"{p.player.key}_{p.shot.key}"));

    public static readonly PlayerTeam Empty = new PlayerTeam(0, Subshot.TYPE_D);

    public PlayerTeam(int which, Subshot sub, params (PlayerConfig, ShotConfig)[] players) {
        this.players = players;
        SelectedIndex = which;
        Subshot = sub;
    }
    public PlayerTeam(Saveable saved) : this(saved.selectedIndex, saved.subshot, saved.players.Select(p => (
        GameManagement.References.FindPlayer(p.playerKey), 
        GameManagement.References.FindShot(p.shotKey))).ToArray()) { }

    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Saveable {
        public (string playerKey, string shotKey)[] players { get; set; }
        public int selectedIndex { get; set; }
        public Subshot subshot { get; set; }

        public Saveable(PlayerTeam team) {
            players = team.players.Select(p => (p.player.key, p.shot.key)).ToArray();
            selectedIndex = team.SelectedIndex;
            subshot = team.Subshot;
        }
    }
}
}