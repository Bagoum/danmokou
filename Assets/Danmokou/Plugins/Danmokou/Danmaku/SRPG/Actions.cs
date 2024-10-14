using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.SRPG.Nodes;
using UnityEngine;

namespace Danmokou.SRPG.Actions {
public interface IUnitAction {
    void Apply(GameState gs);
    void Unapply(GameState gs);
}


public record NewUnit(Node At, Unit Unit) : IUnitAction {
    void IUnitAction.Apply(GameState gs) {
        if (At.Unit != null)
            throw new Exception($"Cannot instantiate new unit at occupied node {At}");
        if (Unit.Location != null)
            throw new Exception($"Unit {Unit} cannot be re-instantiated");
        gs.Units.Add(Unit);
        gs.ActiveRealizer?.Instantiate(this);
        Unit.SetLocationTo(At);
    }

    void IUnitAction.Unapply(GameState gs) {
        Unit.SetLocationTo(null);
        gs.Units.Remove(Unit);
    }
}

public record MoveUnit(Node From, Node To, Unit Unit, List<Node>? Path) : IUnitAction {
    void IUnitAction.Apply(GameState gs) {
        Unit.AssertIsAt(From);
        Unit.UpdateStatus(UnitStatus.CanMove, UnitStatus.Exhausted);
        Unit.SetLocationTo(To);
    }

    void IUnitAction.Unapply(GameState gs) {
        Unit.AssertIsAt(To);
        Unit.UpdateStatus(UnitStatus.Exhausted, UnitStatus.CanMove);
        Unit.SetLocationTo(From);
    }

    public override string ToString() => $"MoveUnit {{ {Unit}: {From} -> {To} }}";
}



}