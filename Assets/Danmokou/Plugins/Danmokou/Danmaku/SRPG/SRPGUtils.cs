using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
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
    private static INodeMatchable[]? _nodeTypes;
    public static INodeMatchable[] NodeTypes => _nodeTypes ??= new INodeMatchable[] {
        new Empty(),
        new Grass(),
        new Road(),
        new Water(),
        new Forested.Maker(),
    };
    private static Dictionary<string, INodeMatchable>? _keyToNodeType;
    public static Dictionary<string, INodeMatchable> KeyToNodeType => _keyToNodeType ??= 
        NodeTypes.Where(nt => nt.Key != null).ToDictionary(nt => nt.Key!);
    private static readonly Dictionary<(INodeType, INodeModifier), INodeType> modCache = new();

    public static Node MakeNode(IReadOnlyList<ISRPGNodeMatcher> matchers, Tilemap[] tilemaps, 
        Vector3Int tilemapMin, Vector2Int index) {
        INodeType? typ = null;
        var loc = tilemapMin + (Vector3Int)index;
        foreach (var tm in tilemaps) {
            if (tm.GetTile(loc) is not { } tile)
                continue;
            var matcher = ISRPGNodeMatcher.Match(tile, matchers) ??
                          throw new Exception($"No matched tile for {tile}");
            var tileCons = SRPGUtils.KeyToNodeType[matcher.Key];
            if (tileCons is INodeModifier modder) {
                if (typ is null)
                    throw new Exception($"Cannot use tile modder {modder} with no base tile");
                typ = modCache.TryGetValue((typ, modder), out var nxt) ?
                    nxt :
                    modCache[(typ, modder)] = modder.Modify(typ);
            } else if (tileCons is INodeType rawTyp)
                typ = rawTyp; //overwrite if already exists
            else throw new Exception($"No handling for tile constructor type {tileCons.GetType().RName()}");
        }
        //cellToWorld gets bottom-left position
        var cellBotLeft = tilemaps[0].CellToWorld(loc);
        return new Node(typ ?? new Empty(), index, 
            cellBotLeft + 0.5f * tilemaps[0].cellSize,
            cellBotLeft + tilemaps[0].tileAnchor.PtMul(tilemaps[0].cellSize));
    }

    /// <summary>
    /// Get all the points that are at a grid distance of `dist` from the origin.
    /// <br/>eg. for 2, returns (-2,0),(-1,1),(0,2),(1,1),(2,0),(1,-1),(0,-2),(-1,-1).
    /// </summary>
    public static void PointsAtGridDistance(int dist, List<Vector2Int> ret) {
        for (int x = 0; x < dist; ++x) {
            var y = dist - x;
            ret.Add(new(x, y));
            ret.Add(new(-y, x));
            ret.Add(new(-x, -y));
            ret.Add(new(y, -x));
        }
    }

    /// <summary>
    /// Get all the points that are at a grid distance of between `minDist` and `maxDist` (inclusive) from the origin.
    /// </summary>
    public static void PointsAtGridRange(int minDist, int maxDist, List<Vector2Int> ret) {
        for (int dist = minDist; dist <= maxDist; ++dist)
            PointsAtGridDistance(dist, ret);
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
        //NB: you could combine these into one Dict<V,(double cost, Maybe<V> prev)>, but it's more convenient
        // from the consumer side to separate them and drop `source` from the `prev` dictionary.
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

    public static string Abbrev(this Stat s) => s switch {
        Stat.MaxHP => "HP",
        Stat.CurrHP => "HP",
        Stat.Attack => "Atk",
        Stat.Defense => "Def",
        Stat.Speed => "Spd",
        Stat.Move => "Mov",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    public static LString Name(this Stat s) => s switch {
        Stat.MaxHP => "Max HP",
        Stat.CurrHP => "Current HP",
        Stat.Attack => "Attack",
        Stat.Defense => "Defense",
        Stat.Speed => "Speed",
        Stat.Move => "Movement",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };
    
    public static Stat FromAbbrev(this string s) => s switch {
        "HP" => Stat.MaxHP,
        "Atk" => Stat.Attack,
        "Def" => Stat.Defense,
        "Spd" => Stat.Speed,
        "Mov" => Stat.Move,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, null)
    };

    public static readonly Stat[] AllStats = {
        Stat.MaxHP,
        Stat.CurrHP,
        Stat.Attack,
        Stat.Defense,
        Stat.Speed,
        Stat.Move,
    };
}
}