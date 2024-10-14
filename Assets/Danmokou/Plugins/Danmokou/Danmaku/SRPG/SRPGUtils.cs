using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Functional;
using BagoumLib.Reflection;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.SRPG.Nodes;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Danmokou.SRPG {
public static class SRPGUtils {
    private static ISRPGNodeMatchable[]? _nodeTypes;
    public static ISRPGNodeMatchable[] NodeTypes => _nodeTypes ??= new ISRPGNodeMatchable[] {
        new Empty(),
        new Grass(),
        new Road(),
        new Water(),
        new Forested.Maker(),
    };
    private static Dictionary<string, ISRPGNodeMatchable>? _keyToNodeType;
    public static Dictionary<string, ISRPGNodeMatchable> KeyToNodeType => _keyToNodeType ??= 
        NodeTypes.Where(nt => nt.Key != null).ToDictionary(nt => nt.Key!);

    public static Node MakeNode(IReadOnlyList<ISRPGNodeMatcher> matchers, Tilemap[] tilemaps,
        Vector3Int loc) {
        ISRPGNodeType? typ = null;
        foreach (var tm in tilemaps) {
            if (tm.GetTile(loc) is not { } tile)
                continue;
            var matcher = ISRPGNodeMatcher.Match(tile, matchers) ??
                          throw new Exception($"No matched tile for {tile}");
            var tileCons = SRPGUtils.KeyToNodeType[matcher.Key];
            if (tileCons is ISRPGNodeModifier modder) {
                if (typ is null)
                    throw new Exception($"Cannot use tile modder {modder} with no base tile");
                typ = modder.Modify(typ);
            } else if (tileCons is ISRPGNodeType rawTyp)
                typ = rawTyp; //overwrite if already exists
            else throw new Exception($"No handling for tile constructor type {tileCons.GetType().RName()}");
        }
        //cellToWorld gets bottom-left position
        var cellBotLeft = tilemaps[0].CellToWorld(loc);
        return new Node(typ ?? new Empty(), loc, 
            cellBotLeft + 0.5f * tilemaps[0].cellSize,
            cellBotLeft + tilemaps[0].tileAnchor.PtMul(tilemaps[0].cellSize));
    }

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