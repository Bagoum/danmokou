using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using Danmokou.Core;

namespace Danmokou.Plugins.Danmokou.Utility {

public interface IEdge<V> {
    public V From { get; }
    public V To { get; }
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

public static class SRPGUtils {
    /// <summary>
    /// Remove any cycles in the list of vertices `path` in-place, and return the same list.
    /// </summary>
    public static List<V> PruneCycle<V>(List<V> path) {
        var indices = DictCache<V, int>.Get();
        for (int ii = 0; ii < path.Count; ++ii) {
            var v = path[ii];
            if (indices.TryGetValue(v, out var pi)) {
                path.RemoveRange(pi, ii - pi);
                ii = pi;
            } else
                indices[v] = ii;
            
        }
        DictCache<V, int>.Consign(indices);
        return path;
    }
    
    /// <summary>
    /// Given a dictionary `prev` mapping each node to an optimal previous node, reconstruct the optimal
    ///  path from the source node to `target`.
    /// </summary>
    public static List<V> ReconstructPath<V>(Dictionary<V, V> prev, V target, List<V>? into = null) {
        into ??= ListCache<V>.Get();
        for (V nxt, x = target;; x = nxt) {
            into.Add(x);
            if (!prev.TryGetValue(x, out nxt))
                break;
        }
        into.Reverse();
        return into;
    }
    
    /// <summary>
    /// Run Dijkstra's algorithm to find all optimal paths to nodes from a source node.
    /// </summary>
    /// <param name="source">Source node.</param>
    /// <param name="adjacency">Function to provide adjacent nodes from a given node. Edge costs must be nonnegative.</param>
    /// <param name="costLimit">Maximum cost for paths, beyond which nodes will be considered unreachable.</param>
    public static (Dictionary<V, double> costs, Dictionary<V, V> prev) Dijkstra<V>(V source,
        Action<V, List<(V nxt, double edgeCost)>> adjacency, double? costLimit) {
        var costs = DictCache<V, double>.Get();
        var prev = DictCache<V, V>.Get();
        var fringe = new PriorityQueue<(Maybe<V> mfrom, V curr), double>();
        
        fringe.Enqueue((Maybe<V>.None, source), 0);
        
        var nearby = ListCache<(V, double)>.Get();
        costLimit = Math.Max(0, costLimit ?? double.MaxValue);
        while (fringe.TryDequeue(out var fc, out var costToCurr) && costToCurr <= costLimit) {
            var (mfrom, curr) = fc;
            //Note: a vertex never needs to be re-expanded under Dijkstra,
            // and also under A* if the heuristic is *consistent*.
            // (see https://www.cs.du.edu/~sturtevant/papers/AStar_Inconsistent.pdf)
            if (costs.ContainsKey(curr)) continue;
            if (mfrom.Try(out var from))
                prev[curr] = from;
            costs[curr] = costToCurr;
            nearby.Clear();
            adjacency(curr, nearby);
            foreach (var (nxt, edgeCost) in nearby)
                fringe.Enqueue((curr, nxt), costToCurr + edgeCost);
        }
        return (costs, prev);
    }
}
}