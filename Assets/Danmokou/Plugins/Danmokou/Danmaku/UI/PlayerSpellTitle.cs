using System;
using System.Collections;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class PlayerSpellTitle : CoroutineRegularUpdater {
    private Transform tr = null!;
    
    public TextMeshPro title = null!;
    public SpriteRenderer underline1 = null!;
    public SpriteRenderer underline2 = null!;

    private void Awake() {
        tr = transform;
    }

    public void Initialize(string spellName, Color c1, Color c2, float? displaySeconds = null) {
        title.text = spellName;
        var c = title.color;
        c.a = 0;
        title.color = c;
        c1.a = 0;
        c2.a = 0;
        underline1.color = c1;
        underline2.color = c2;
        RunDroppableRIEnumerator(DisplayMe(displaySeconds ?? 5f));
    }

    private IEnumerator DisplayMe(float displayTime) {
        float elapsed = 0;
        float eR() => elapsed / displayTime;
        var l = tr.localPosition = new Vector2(3.5f, -4.4f);
        float Vel() => (0.4f 
                        + 4.6f * (1 - M.EIOSine(M.RatioC(0, 0.4f, eR())))
                        + 2f * M.EInSine(M.RatioC(0.7f, 1, eR()))) / displayTime;
        float Opacity() => M.EIOSine(M.RatioC(0, 0.35f, eR())) * (1 - M.EInSine(M.RatioC(0.7f, 1, eR())));
        float textScale() => 1 + 0.4f * M.EInSine(M.RatioC(0.75f, 1, eR()));

        var ct = title.color;
        var c1 = underline1.color;
        var c2 = underline2.color;
        for (; elapsed < displayTime; elapsed += ETime.FRAME_TIME) {
            l.y += Vel() * ETime.FRAME_TIME;
            tr.localPosition = l;
            ct.a = c1.a = c2.a = Opacity();
            title.color = ct;
            underline1.color = c1;
            underline2.color = c2;
            var s = textScale();
            title.transform.localScale = new Vector3(s, s, s);
            yield return null;
        }
        Destroy(gameObject);
    }
}
}