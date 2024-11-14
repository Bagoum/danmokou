using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Player {
[Serializable]
public struct GridEntry {
    public Vector2Int location;
    public SpriteRenderer sr;
}
public class MovementGridDisplay : RegularUpdater {
    private class Entry {
        public readonly SpriteRenderer sr;
        public readonly PushLerper<Color> color = new(0.15f);
        public readonly Transform tr;

        public Entry(MovementGridDisplay src, SpriteRenderer sr) {
            this.sr = sr;
            this.tr = sr.transform;
            src.AddToken(color.Subscribe(c => sr.color = c));
        }
    }
    public GridEntry[] entries = null!;
    private Dictionary<Vector2Int, Entry> grid = null!;

    public Sprite unselSprite = null!;
    public Sprite selSprite = null!;
    public Color unselColor = Color.black;
    public Color selColor = Color.white;

    private void Awake() {
        grid = entries.ToDictionary(e => e.location, e => new Entry(this, e.sr));
        SelectEntry(null);
    }

    public override void RegularUpdate() {
        foreach (var e in grid.Values)
            e.color.Update(ETime.FRAME_TIME);
    }

    public void SelectEntry(Vector2Int? coord) {
        foreach (var (loc, sr) in grid) {
            sr.sr.sprite = (loc == coord) ? selSprite : unselSprite;
            sr.color.PushIfNotSame((loc == coord) ? selColor : unselColor);
        }
    }

    public bool HasCoord(Vector2Int coord) => grid.ContainsKey(coord);

    public Vector2 GetLocation(Vector2Int coord) => grid[coord].tr.position;
}
}