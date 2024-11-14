using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
using Danmokou.Core;
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
    public Faction ActingFaction => TurnOrder[ActingFactionIdx];
    public int ActingFactionIdx { get; private set; }
    public Faction[] TurnOrder { get; }
    public int TurnNumber { get; private set; }
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

    public void CheckForTurnEnd(IGameDiff causer, List<IGameDiff> caused) {
        if (IsSimulating) return;
        foreach (var u in Units)
            if (u.Team == ActingFaction && u.Status is not UnitStatus.Exhausted)
                return;
        caused.Add(MakeFactionTurnSwitch(causer));
    }

    private SwitchFactionTurn MakeFactionTurnSwitch(IGameDiff cause) {
        if (ActingFactionIdx == TurnOrder.Length - 1) {
            return new(ActingFactionIdx, 0, TurnNumber + 1) { CausedBy = cause };
        } else {
            return new(ActingFactionIdx, ActingFactionIdx+1, TurnNumber) { CausedBy = cause };
        }
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

    public void AddDiffFast(IGameDiff diff) {
        var caused = ListCache<IGameDiff>.Get();
        _AddDiffFast(diff, caused);
        ListCache<IGameDiff>.Consign(caused);
    }

    private void _AddDiffFast(IGameDiff diff, List<IGameDiff> caused) {
        AnimatingGuard();
        MustActUnitGuard(diff);
        Logs.Log($"Quickadding diff: {diff}");
        var startIdx = caused.Count;
        Actions.Add(diff);
        diff.PreApply(this, caused);
        diff.Apply(this, caused);
        foreach (var u in Units) 
        foreach (var eff in u.Stats.Mods)
            eff.ProcessCausation(diff, caused);
        var endIdx = caused.Count;
        for (int ii = startIdx; ii < endIdx; ++ii) {
            var nxt = caused[ii];
            nxt.CausedBy = diff;
            _AddDiffFast(nxt, caused);
        }
        caused.RemoveRange(startIdx, endIdx - startIdx);
    }

    public async Task<Completion> AddDiff(IGameDiff diff, ICancellee cT) {
        var caused = ListCache<IGameDiff>.Get();
        try {
            return await _AddDiff(diff, cT, caused);
        } finally {
            ListCache<IGameDiff>.Consign(caused);
        }
    }

    private async Task<Completion> _AddDiff(IGameDiff diff, ICancellee cT, List<IGameDiff> caused) {
        AnimatingGuard();
        MustActUnitGuard(diff);
        if (IsSimulating || cT.IsSoftCancelled()) {
            _AddDiffFast(diff, caused);
            return Completion.SoftSkip;
        } else {
            Logs.Log($"Animating diff: {diff}");
            var startIdx = caused.Count;
            diff.PreApply(this, caused);
            try {
                ++animCt;
                if (Realizer.Animate(diff, cT, new(caused, startIdx)) is { IsCompletedSuccessfully: false } t)
                    await t;
            } catch (OperationCanceledException) {
                //ignore cancellations within animation step unless cT is hard-cancelled (below)
            } finally {
                --animCt;
            }
            cT.ThrowIfHardCancelled();
            Actions.Add(diff);
            diff.Apply(this, caused);
            foreach (var u in Units) 
            foreach (var eff in u.Stats.Mods)
                eff.ProcessCausation(diff, caused);
            var endIdx = caused.Count;
            for (int ii = startIdx; ii < endIdx; ++ii) {
                var nxt = caused[ii];
                nxt.CausedBy = diff;
                await _AddDiff(nxt, cT, caused);
            }
            caused.RemoveRange(startIdx, endIdx - startIdx);
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

    private void UpdateFaction(int nxt, bool isRevert, bool isFirst) {
        //At the end of a faction turn, all units from that faction must be in Exhausted state
        // (to ensure revertability)
        //TODO ensure that all non-exhausted units are given an Exhaust action if the turn is manually ended
        var (currReqState, nxtReqState) = (UnitStatus.Exhausted, UnitStatus.CanMove);
        if (isRevert)
            (currReqState, nxtReqState) = (nxtReqState, currReqState);
        var nxtFac = TurnOrder[nxt];
        foreach (var u in Units)
            u.UpdateStatus(
                isFirst ? null : (u.Team == ActingFaction ? currReqState : UnitStatus.NotMyTurn), 
                u.Team == nxtFac ? nxtReqState : UnitStatus.NotMyTurn);
        ActingFactionIdx = nxt;
    }

    public record StartGame(int FirstFactionIdx) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
            gs.TurnNumber = 1;
            gs.UpdateFaction(FirstFactionIdx, false, true);
        }

        void IGameDiff.Unapply(GameState gs) {
            throw new Exception("Cannot undo start game diff");
        }
    }
    
    public record SwitchFactionTurn(int FromIdx, int NextIdx, int TurnNo) : IGameDiff {
        public IGameDiff? CausedBy { get; set; }
        void IGameDiff.Apply(GameState gs, List<IGameDiff> caused) {
            if (gs.ActingFactionIdx != FromIdx)
                throw new Exception($"The current faction must be {gs.TurnOrder[FromIdx]}, but is {gs.ActingFaction}");
            gs.TurnNumber = TurnNo;
            gs.UpdateFaction(NextIdx, false, false);
        }

        void IGameDiff.Unapply(GameState gs) {
            if (gs.ActingFactionIdx != NextIdx)
                throw new Exception($"The current faction must be {gs.TurnOrder[NextIdx]}, but is {gs.ActingFaction}");
            gs.UpdateFaction(FromIdx, true, false);
        }
    }
}


}