using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using Danmokou.SRPG.Actions;
using Danmokou.SRPG.Nodes;
using UnityEngine;

namespace Danmokou.SRPG {

public record Faction(LString Name) {
    public GameState State { get; set; } = null!;
    public bool FlipSprite { get; init; } = false;

    public override string ToString() => $"Faction {Name}";
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
    //MustAct //TODO setup MustAct
}

public class Unit {
    public Faction Team { get; }
    public string Key { get; }
    public LString Name { get; }
    public int Move { get; }
    public Node? Location { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.NotMyTurn;
    
    public Unit(Faction team, string key, int move, LString? name = null) {
        this.Team = team;
        this.Key = key;
        this.Name = name ?? key;
        this.Move = move;
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
        Team.State.ActiveRealizer?.SetUnitLocation(this, prevLoc, target);
    }

    public override string ToString() => $"{Name} ({Team})";
}

public class GameState {
    public Graph<Edge, Node> Graph { get; }
    public HashSet<Unit> Units { get; } = new();
    private List<IUnitAction> Actions { get; } = new();
    public IUnitAction? MostRecentAction => Actions.Count > 0 ? Actions[^1] : null;
    private readonly Stack<int> checkpoints = new();
    public bool IsSimulating => checkpoints.Count > 0;
    public Faction? ActingFaction { get; private set; }
    public Faction[] TurnOrder { get; }
    private IStateRealizer Realizer { get; }
    public IStateRealizer? ActiveRealizer => IsSimulating ? null : Realizer;
    
    public GameState(IStateRealizer realizer, IEnumerable<Node> nodes, IEnumerable<Edge> edges, params Faction[] turnOrder) {
        Realizer = realizer;
        Graph = new(nodes, edges);
        foreach (var n in Graph.Nodes)
            n.Graph = Graph;
        TurnOrder = turnOrder;
        foreach (var f in TurnOrder)
            f.State = this;
    }

    private bool ShouldAutoEndFactionTurn() {
        if (ActingFaction is null || IsSimulating) return false;
        foreach (var u in Units)
            if (u.Team == ActingFaction && u.Status is not UnitStatus.Exhausted)
                return false;
        return true;
    }

    private void AutoEndFactionTurnFast() {
        var idx = TurnOrder.IndexOf(ActingFaction!);
        AddActionFast(new SwitchFactionTurn(ActingFaction!, TurnOrder.ModIndex(idx + 1)));
    }
    private async Task AutoEndFactionTurn(ICancellee cT) {
        var idx = TurnOrder.IndexOf(ActingFaction!);
        await AddAction(new SwitchFactionTurn(ActingFaction!, TurnOrder.ModIndex(idx + 1)), cT);
    }

    public void AddActionFast(IUnitAction action) {
        Actions.Add(action);
        action.Apply(this);
        if (ShouldAutoEndFactionTurn())
            AutoEndFactionTurnFast();
    }

    public async Task<Completion> AddAction(IUnitAction action, ICancellee cT) {
        if (IsSimulating || cT.IsSoftCancelled()) {
            AddActionFast(action);
            return Completion.SoftSkip;
        } else {
            Actions.Add(action);
            if (Realizer.Animate(action, cT) is { IsCompletedSuccessfully: false } t)
                await t;
            cT.ThrowIfHardCancelled();
            action.Apply(this);
            if (ShouldAutoEndFactionTurn())
                await AutoEndFactionTurn(cT);
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
        if (IsSimulating) throw new Exception("Cannot undo operation while simulating");
        if (Actions.Count == 0 || Actions[^1] is StartGame) return false;
        Actions.Pop().Unapply(this);
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

    public record StartGame(Faction FirstFaction) : IUnitAction {
        void IUnitAction.Apply(GameState gs) {
            if (gs.ActingFaction != null)
                throw new Exception("Cannot start game; game is already started");
            gs.UpdateFaction(FirstFaction, false);
        }

        void IUnitAction.Unapply(GameState gs) {
            if (gs.ActingFaction != null)
                throw new Exception("Cannot unstart game; game has not yet been started");
            gs.UpdateFaction(null, true);
        }
    }
    
    public record SwitchFactionTurn(Faction From, Faction To) : IUnitAction {
        void IUnitAction.Apply(GameState gs) {
            if (gs.ActingFaction != From)
                throw new Exception($"The current faction must be {From}, but is {gs.ActingFaction}");
            gs.UpdateFaction(To, false);
        }

        void IUnitAction.Unapply(GameState gs) {
            if (gs.ActingFaction != To)
                throw new Exception($"The current faction must be {To}, but is {gs.ActingFaction}");
            gs.UpdateFaction(From, true);
        }
    }
}


}