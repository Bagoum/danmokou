using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Danmokou.Scriptables {
public interface ISRPGNodeMatcher {
    public string Key { get; }
    public bool TileMatches(TileBase tile);

    public static ISRPGNodeMatcher? Match(TileBase tile, IReadOnlyList<ISRPGNodeMatcher> matchers) {
        for (int ii = 0; ii < matchers.Count; ++ii)
            if (matchers[ii].TileMatches(tile))
                return matchers[ii];
        return null;
    }
}

[CreateAssetMenu(menuName = "SRPG/Node Matcher")]
public class SRPGNodeMatcher : ScriptableObject, ISRPGNodeMatcher {
    [field:SerializeField]
    public string Key { get; set; } = "";
    public Sprite[] sprites = null!;
    
    public bool TileMatches(TileBase tile) {
        if (tile is not Tile t)
            throw new Exception($"TileBase {tile} is not a Tile");
        return sprites.Contains(t.sprite);
    }
}
}