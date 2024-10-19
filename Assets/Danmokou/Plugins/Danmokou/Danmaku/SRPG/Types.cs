using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using Danmokou.SRPG.Diffs;
using Danmokou.SRPG.Nodes;
using Newtonsoft.Json;
using UnityEngine;

namespace Danmokou.SRPG {

public class Faction {
    public LString Name { get; }
    public Color Color { get; }
    public GameState State { get; set; } = null!;
    public bool FlipSprite { get; init; } = false;
    
    public Faction(LString Name, Color Color) {
        this.Name = Name;
        this.Color = Color;
    }

    public override string ToString() => Name;
}

public enum UnitStatus {
    /// <summary>
    /// The unit cannot act because the current turn belongs to a different faction.
    /// </summary>
    NotMyTurn,
    /// <summary>
    /// The unit cannot act because its turn has been cancelled or it has already acted on this faction turn.
    /// </summary>
    Exhausted,
    /// <summary>
    /// The unit can move and act.
    /// </summary>
    CanMove,
    /// <summary>
    /// The unit has moved and now must act.
    /// </summary>
    MustAct
}

public class Unit {
    public Faction Team { get; }
    [JsonIgnore] public GameState State => Team.State;
    public string Key { get; }
    public StatBlock Stats { get; }
    public LString Name { get; init; }
    public Node? Location { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.NotMyTurn;
    public IUnitSkill[] Skills { get; }
    [JsonIgnore] public IEnumerable<IUnitSkill> AttackSkills => Skills.Where(s => s.Type is UnitSkillType.Attack);
    
    public Unit(Faction team, string key, StatBlock stats, params IUnitSkill[] skills) {
        this.Team = team;
        this.Key = key;
        this.Stats = stats;
        this.Skills = skills;
        this.Name = key;
    }

    public void AssertIsAt(Node target) {
        if (Location != target || target?.Unit != this)
            throw new Exception($"Unit {this} is not at location {target}");
    }
    public void UpdateStatus(UnitStatus? req, UnitStatus nxt) {
        if (req != null && Status != req)
            throw new Exception($"Unit {this} must have status {req}, but is currently {Status}");
        Status = nxt;
    }

    public void SetLocationTo(Node? target) {
        if (Location != null)
            Location.Unit = null;
        var prevLoc = Location;
        Location = target;
        if (Location != null)
            Location.Unit = this;
        State.ActiveRealizer?.SetUnitLocation(this, prevLoc, target);
    }

    public override string ToString() => $"{Name} ({Team})";
}

public class GameState {
    public Node[,] Map { get; }
    public int Height { get; }
    public int Width { get; }
    public Graph<Edge, Node> Graph { get; }
    public HashSet<Unit> Units { get; } = new();
    private List<IGameDiff> Actions { get; } = new();
    public IGameDiff? MostRecentAction => Actions.Count > 0 ? Actions[^1] : null;
    public int NActions => Actions.Count;
    private readonly Stack<int> checkpoints = new();
    public bool IsSimulating => checkpoints.Count > 0;
    public Faction? ActingFaction { get; private set; }
    public Faction[] TurnOrder { get; }
    private IStateRealizer Realizer { get; }
    public IStateRealizer? ActiveRealizer => IsSimulating ? null : Realizer;
    private int animCt = 0;
    public bool IsAnimating => animCt > 0;
    public Unit? MustActUnit {
        get {
            foreach (var u in Units)
                if (u.Status == UnitStatus.MustAct)
                    return u;
            return null;
        }
    }

    public GameState(IStateRealizer realizer, Node[,] nodes, IEnumerable<Edge> edges, params Faction[] turnOrder) {
        Realizer = realizer;
        this.Map = nodes;
        Height = Map.GetLength(0);
        Width = Map.GetLength(1);
        Graph = new(nodes.Cast<Node>(), edges);
        foreach (var n in Graph.Nodes)
            n.Graph = Graph;
        TurnOrder = turnOrder;
        foreach (var f in TurnOrder)
            f.State = this;
    }

    /// <summary>
    /// Get the node at the given index in the map if the index is in bounds. If it is out of bounds, return null.
    /// </summary>
    public Node? TryNodeAt(Vector2Int index) => 
        index.x >= 0 && index.x < Width && index.y >= 0 && index.y < Height ?
            NodeAt(index) :
            null;
    
    /// <summary>
    /// Get the node at the given index in the map.
    /// </summary>
    public Node NodeAt(Vector2Int index) => Map[index.y, index.x];

    private bool ShouldAutoEndFactionTurn() {
        if (ActingFaction is null || IsSimulating) return false;
        foreach (var u in Units)
            if (u.Team == ActingFaction && u.Status is not UnitStatus.Exhausted)
                return false;
        return true;
    }

    private void AutoEndFactionTurnFast(IGameDiff cause) {
        var idx = TurnOrder.IndexOf(ActingFaction!);
        AddActionFast(new SwitchFactionTurn(ActingFaction!, TurnOrder.ModIndex(idx + 1)) { CausedBy = cause });
    }
    private async Task AutoEndFactionTurn(IGameDiff cause, ICancellee cT) {
        var idx = TurnOrder.IndexOf(ActingFaction!);
        await AddAction(new SwitchFactionTurn(ActingFaction!, TurnOrder.ModIndex(idx + 1)) { CausedBy = cause }, cT);
    }

    //For animating actions, Apply is not called until after the animation is finished
    // (in order to maximally preserve existing state during the animation, eg. for Exhaustion during actions)
    //As such, it would cause problems if you added another action while a previous one was animating.
    private void AnimatingGuard([CallerMemberName] string? caller = null) {
        if (IsAnimating)
            throw new Exception($"Operation {caller} cannot be executed during animation");
    }
    
    private void MustActUnitGuard(IGameDiff diff) {
        if (diff?.CausedBy is null && MustActUnit is { } u && (diff is not IUnitSkillDiff ua || ua.Unit != u))
            throw new Exception($"The unit {u} must act next; uncaused action {diff} cannot be executed");
    }

    public void AddActionFast(IGameDiff diff) {
        AnimatingGuard();
        MustActUnitGuard(diff);
        Actions.Add(diff);
        if (diff.Apply(this) is { } caused) {
            foreach (var nxt in caused) {
                nxt.CausedBy = diff;
                AddActionFast(nxt);
            }
            ListCache<IGameDiff>.Consign(caused);
        }
        foreach (var u in Units) 
        foreach (var eff in u.Stats.Mods)
            if (eff.ProcessCausation(diff) is { } nxt) {
                nxt.CausedBy = diff;
                AddActionFast(nxt);
            }
        if (ShouldAutoEndFactionTurn())
            AutoEndFactionTurnFast(diff);
    }

    public async Task<Completion> AddAction(IGameDiff diff, ICancellee cT) {
        AnimatingGuard();
        MustActUnitGuard(diff);
        if (IsSimulating || cT.IsSoftCancelled()) {
            AddActionFast(diff);
            return Completion.SoftSkip;
        } else {
            try {
                ++animCt;
                if (Realizer.Animate(diff, cT) is { IsCompletedSuccessfully: false } t)
                    await t;
                cT.ThrowIfHardCancelled();
            } finally {
                --animCt;
            }
            Actions.Add(diff);
            //Caused effects native to the action, eg. an attack that applies a stat debuff
            if (diff.Apply(this) is { } caused) {
                foreach (var nxt in caused) {
                    nxt.CausedBy = diff;
                    await AddAction(nxt, cT);
                }
                ListCache<IGameDiff>.Consign(caused);
            }
            //Caused effects by interactions, eg. a stat debuff with 1 turn duration
            // is deleted due to SwitchFactionTurn action
            foreach (var u in Units) 
            foreach (var eff in u.Stats.Mods)
                if (eff.ProcessCausation(diff) is { } nxt) {
                    nxt.CausedBy = diff;
                    await AddAction(nxt, cT);
                }
            if (ShouldAutoEndFactionTurn())
                await AutoEndFactionTurn(diff, cT);
            return cT.ToCompletion();
        }
    }

    public void Checkpoint() => checkpoints.Push(Actions.Count);

    public void RevertToCheckpoint() {
        var ct = checkpoints.Peek();
        while (Actions.Count > ct)
            Actions.Pop().Unapply(this);
        checkpoints.Pop();
    }

    public bool Undo() {
        AnimatingGuard();
        if (IsSimulating) throw new Exception("Cannot undo operation while simulating");
        if (Actions.Count == 0 || Actions[^1] is StartGame) return false;
        IGameDiff nxt;
        do {
            (nxt = Actions.Pop()).Unapply(this);
            //Caused actions must be unapplied with their causing action
            //For simplicity, undo unit post-move actions with the move action (otherwise UI is difficult to handle)
        } while (nxt.CausedBy is not null || nxt is IUnitSkillDiff);
        return true;
    }

    private void UpdateFaction(Faction? nxt, bool isRevert) {
        //At the end of a faction turn, all units from that faction must be in Exhausted state
        // (to ensure revertability)
        //TODO ensure that all non-exhausted units are given an Exhaust action if the turn is manually ended
        var (currReqState, nxtReqState) = (UnitStatus.Exhausted, UnitStatus.CanMove);
        if (isRevert)
            (currReqState, nxtReqState) = (nxtReqState, currReqState);
        foreach (var u in Units)
            u.UpdateStatus(
                u.Team == ActingFaction ? currReqState : UnitStatus.NotMyTurn, 
                u.Team == nxt ? nxtReqState : UnitStatus.NotMyTurn);
        ActingFaction = nxt;
    }

    public record StartGame(Faction FirstFaction) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        List<IGameDiff>? IGameDiff.Apply(GameState gs) {
            if (gs.ActingFaction != null)
                throw new Exception("Cannot start game; game is already started");
            gs.UpdateFaction(FirstFaction, false);
            return null;
        }

        void IGameDiff.Unapply(GameState gs) {
            if (gs.ActingFaction != null)
                throw new Exception("Cannot unstart game; game has not yet been started");
            gs.UpdateFaction(null, true);
        }
    }
    
    public record SwitchFactionTurn(Faction From, Faction To) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        List<IGameDiff>? IGameDiff.Apply(GameState gs) {
            if (gs.ActingFaction != From)
                throw new Exception($"The current faction must be {From}, but is {gs.ActingFaction}");
            gs.UpdateFaction(To, false);
            return null;
        }

        void IGameDiff.Unapply(GameState gs) {
            if (gs.ActingFaction != To)
                throw new Exception($"The current faction must be {To}, but is {gs.ActingFaction}");
            gs.UpdateFaction(From, true);
        }
    }
}


}