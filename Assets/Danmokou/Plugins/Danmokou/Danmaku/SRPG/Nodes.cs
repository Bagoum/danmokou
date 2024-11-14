using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.SRPG.Nodes {
public interface INodeMatchable {
    string? Key => this.GetType().RName();
}
/// <summary>
/// A data singletons that represent a tile type in the SRPG context.
/// <br/>eg. a grass tile, a grass tile with forest, a road tile, etc.
/// </summary>
public interface INodeType : INodeMatchable {
    LString Description { get; }
    public float Cost(Unit u);
    public int Heal => 0;
    public int Power => 0;
    public int Shield => 0;
        
    public static float MAXCOST => M.IntFloatMax;
}
/// <summary>
/// A data singleton that represents a modded tile type in the SRPG context.
/// <br/>eg. a grass tile with forest.
/// </summary>
public interface IModdedNodeType : INodeType {
    string? INodeMatchable.Key => null;
}

/// <summary>
/// A modifier that creates a new <see cref="INodeType"/> from a base <see cref="INodeType"/>.
/// <br/>eg. <see cref="Forested"/>.<see cref="Forested.Maker"/> creates a <see cref="Forested"/> tile from any basic tile.
/// </summary>
public interface INodeModifier : INodeMatchable {
    public INodeType Modify(INodeType baseNode);
}

public class Empty : INodeType {
    public string Key => "null";
    public LString Description => "Empty";

    public float Cost(Unit u) => INodeType.MAXCOST;
}

public record Grass : INodeType {
    public LString Description { get; } = new LText("Plain", (Locales.JP, "草原"));
    
    public float Cost(Unit u) => 1;
}

public record Road : INodeType {
    public LString Description { get; } = new LText("Road", (Locales.JP, "道路"));
    
    public float Cost(Unit u) => u.Movement.HasFlag(MovementFlags.Flying) ? 1 : 0.8f;
}

public record Water : INodeType {
    public LString Description { get; } = new LText("Water", (Locales.JP, "水域"));
    
    public float Cost(Unit u) => u.Movement.HasFlag(MovementFlags.Flying) ? 1 : INodeType.MAXCOST;
}

public record Forested(INodeType Base) : IModdedNodeType {
    public class Maker : INodeModifier {
        public INodeType Modify(INodeType baseNode) => new Forested(baseNode);
    }
    public LString Description { get; } = Base is Grass ?
        new LText("Forest", (Locales.JP, "森")) :
        new LText($"Forested {Base.Description}", (Locales.JP, $"森の{Base.Description}"));

    public float Cost(Unit u) => 1 + Base.Cost(u);
}


public class Node {
    public INodeType Type { get; }
    public Vector2Int Index { get; }
    public Vector3 CellAnchor { get; }
    public Vector3 CellCenter { get; }
    public Graph<Edge, Node> Graph { get; set; } = null!;
    public Unit? Unit { get; set; }
    
    public Node(INodeType type, Vector2Int index, Vector3 cellCenter, Vector3 cellAnchor) {
        Type = type;
        this.Index = index;
        this.CellCenter = cellCenter;
        this.CellAnchor = cellAnchor;
    }

    public float EntryCost(Unit u) => Type.Cost(u);
    public float ExitCost(Unit u) => 0;
    public LString Description => Type.Description;

    public void OutgoingEdges(Unit u, List<(Node nxt, double cost)> edges) {
        foreach (var e in Graph.Outgoing(this))
            edges.Add((e.To, e.Cost(u)));
    }

    public override string ToString() => $"{Type.Description}<{Index.x},{Index.y}>";
}

}