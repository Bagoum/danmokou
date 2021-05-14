using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Scriptables;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class FadeWhenPlayerNearby : CoroutineRegularUpdater {
    public SpriteRenderer[] sprites = null!;
    public TextMeshPro[] texts = null!;

    public SOPlayerHitbox player = null!;
    public Vector2 radius;
    public Vector2 fade;

    private Transform tr = null!;

    private void Awake() {
        tr = transform;
        RunDroppableRIEnumerator(CheckPlayer());
    }

    private IEnumerator CheckPlayer() {
        while (true) {
            float dist = (player.location - (Vector2) tr.position).magnitude;
            float ratio = Mathf.Clamp01((dist - radius.x) / (radius.y - radius.x));
            float opacity = Mathf.Lerp(fade.x, fade.y, ratio);

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