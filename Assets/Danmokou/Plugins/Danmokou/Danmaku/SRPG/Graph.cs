using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.SRPG.Nodes;

namespace Danmokou.SRPG {

public interface IEdge<V> {
    public V From { get; }
    public V To { get; }
    //NB: Edge costs are not fixed; they may vary according to who is crossing.
}

public record Edge(Node From, Node To) : IEdge<Node> {
    public int Direction { get; init; } = 0;
    public float Cost(Unit u) => From.ExitCost(u) + To.EntryCost(u);
}


public class Graph<E, V>: IDisposable where V: notnull where E: IEdge<V> {
    public V[] Nodes { get; }
    private readonly Dictionary<V, List<E>> outgoing = DictCache<V, List<E>>.Get();
    private readonly Dictionary<V, List<E>> incoming = DictCache<V, List<E>>.Get();
    private readonly Dictionary<(V, V), E> edges = DictCache<(V,V), E>.Get(); //assumes <=1 edge per vertex pair
    private static readonly List<E> EmptyList = new();

    public Graph(IEnumerable<V> Nodes, IEnumerable<E> Edges) {
        this.Nodes = Nodes.ToArray();
        foreach (var e in Edges) {
            outgoing.AddToList(e.From, e);
            incoming.AddToList(e.To, e);
            edges[(e.From, e.To)] = e;
        }
    }
    public IReadOnlyList<E> Outgoing(V node) => outgoing.GetValueOrDefault(node, EmptyList);
    public IReadOnlyList<E> Incoming(V node) => incoming.GetValueOrDefault(node, EmptyList);
    public E? TryFindEdge(V from, V to) => edges.GetValueOrDefault((from, to));

    public E FindEdge(V from, V to) =>
        TryFindEdge(from, to) ?? throw new Exception($"No edge exists between {from} and {to}");

    public void Dispose() {
        outgoing.ConsignRecursive();
        incoming.ConsignRecursive();
        DictCache<(V,V), E>.Consign(edges);
    }
}


}