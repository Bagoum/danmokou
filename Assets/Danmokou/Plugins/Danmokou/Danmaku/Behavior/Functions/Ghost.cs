using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior.Functions {
public class Ghost : CoroutineRegularUpdater {
    private SpriteRenderer sr = null!;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
    }

    public void Initialize(Sprite s, Vector3 location, float fade_time) {
        transform.position = location;
        sr.sprite = s;
        RunDroppableRIEnumerator(GhostFade(fade_time));
    }

    private IEnumerator GhostFade(float time) {
        Color c = sr.color;
        Color bc = c;
        for (float t = 0; t < time; t += ETime.FRAME_TIME) {
            yield return null;
            bc.a = c.a * (1 - t / time);
            sr.color = bc;
        }
        Destroy(gameObject);
    }
}
}
