using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.Reflection;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.SRPG.Nodes {
public interface ISRPGNodeMatchable {
    string? Key => this.GetType().RName();
}
/// <summary>
/// A data singletons that represent a tile type in the SRPG context.
/// <br/>eg. a grass tile, a grass tile with forest, a road tile, etc.
/// </summary>
public interface ISRPGNodeType : ISRPGNodeMatchable {
    LString Description { get; }
    public int Cost(Unit u);
}
/// <summary>
/// A data singleton that represents a modded tile type in the SRPG context.
/// <br/>eg. a grass tile with forest.
/// </summary>
public interface IModdedSPRGNodeType : ISRPGNodeType {
    string? ISRPGNodeMatchable.Key => null;
}

/// <summary>
/// A modifier that creates a new <see cref="ISRPGNodeType"/> from a base <see cref="ISRPGNodeType"/>.
/// <br/>eg. <see cref="Forested"/>.<see cref="Forested.Maker"/> creates a <see cref="Forested"/> tile from any basic tile.
/// </summary>
public interface ISRPGNodeModifier : ISRPGNodeMatchable {
    public ISRPGNodeType? Modify(ISRPGNodeType baseNode);
}

public class Empty : ISRPGNodeType {
    public string Key => "null";
    public LString Description => "Empty";

    public int Cost(Unit u) => 1000000;
}

public record CustomNodeType(int cost) : ISRPGNodeType {
    public string Key => throw new Exception();
    public LString Description => "CUSTOM";
    
    public int Cost(Unit u) => cost;
}

public record Grass : ISRPGNodeType {
    public LString Description { get; } = new LText("Plain", (Locales.JP, "草原"));
    
    public int Cost(Unit u) => 1;
}

public record Road : ISRPGNodeType {
    public LString Description { get; } = new LText("Road", (Locales.JP, "道路"));
    
    public int Cost(Unit u) => 1;
}

public record Water : ISRPGNodeType {
    public LString Description { get; } = new LText("Water", (Locales.JP, "水域"));
    
    public int Cost(Unit u) => 3;
}

public record Forested(ISRPGNodeType Base) : ISRPGNodeType {
    public class Maker : ISRPGNodeModifier {
        public ISRPGNodeType? Modify(ISRPGNodeType baseNode) => new Forested(baseNode);
    }
    string? ISRPGNodeMatchable.Key => null;
    public LString Description { get; } = Base is Grass ?
        new LText("Forest", (Locales.JP, "森")) :
        new LText($"Forested {Base.Description}", (Locales.JP, $"森の{Base.Description}"));

    public int Cost(Unit u) => 2 + Base.Cost(u);
}


public class Node {
    public ISRPGNodeType Type { get; }
    public Vector2Int Index { get; }
    public Vector3 CellAnchor { get; }
    public Vector3 CellCenter { get; }
    public Graph<Edge, Node> Graph { get; set; } = null!;
    public Unit? Unit { get; set; }
    
    public Node(ISRPGNodeType type, Vector2Int index, Vector3 cellCenter, Vector3 cellAnchor) {
        Type = type;
        this.Index = index;
        this.CellCenter = cellCenter;
        this.CellAnchor = cellAnchor;
    }

    public int EntryCost(Unit u) => Type.Cost(u);
    public int ExitCost(Unit u) => 0;
    public string Description => Type.Description;

    public void OutgoingEdges(Unit u, List<(Node nxt, double cost)> edges) {
        foreach (var e in Graph.Outgoing(this))
            edges.Add((e.To, e.Cost(u)));
    }

    public override string ToString() => $"{Type.Description}<{Index.x},{Index.y}>";
}

}