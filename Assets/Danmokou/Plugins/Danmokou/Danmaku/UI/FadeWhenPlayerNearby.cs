using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using Danmokou.Scriptables;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class FadeWhenPlayerNearby : CoroutineRegularUpdater {
    public SpriteRenderer[] sprites = null!;
    public TextMeshPro[] texts = null!;

    public Vector2 radius;
    public Vector2 fade;

    private Transform tr = null!;
    private PlayerController player = null!;

    private void Awake() {
        tr = transform;
    }

    public override void FirstFrame() {
        var p = ServiceLocator.FindOrNull<PlayerController>();
        if (p != null) {
            player = p;
            RunDroppableRIEnumerator(CheckPlayer());
        }
    }

    private IEnumerator CheckPlayer() {
        while (true) {
            float dist = (player.Location - (Vector2) tr.position).magnitude;
            float opacity = M.Lerp(radius.x, radius.y, dist, fade.x, fade.y);

            for (int ii = 0; ii < sprites.Length; ++ii) {
                var c = sprites[ii].color;
                c.a = opacity;
                sprites[ii].color = c;
            }
            for (int ii = 0; ii < texts.Length; ++ii) {
                var c = texts[ii].color;
                c.a = opacity;
                texts[ii].color = c;
            }

            yield return null;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}
}