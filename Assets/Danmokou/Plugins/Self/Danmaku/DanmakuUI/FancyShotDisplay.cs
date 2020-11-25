using System.Collections;
using System.Collections.Generic;
using Danmaku;
using Danmaku.DanmakuUI;
using DMath;
using TMPro;
using UnityEngine;

public class FancyShotDisplay : FancyDisplay {
    public TextMeshPro shotTitle;
    public TextMeshPro shotDescription;

    public void SetShot(PlayerConfig p, int index, ShotConfig s, Enums.Subshot sub) {
        shotTitle.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        shotDescription.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        var type = s.isMultiShot ? $"Multishot Variant {sub.Describe()}" : $"Type {index.ToABC()}";
        var ss = s.GetSubshot(sub);
        shotTitle.text = s.isMultiShot ? $"Multishot:\n{ss.title}" : ss.title;
        shotDescription.text = $"{type} / {ss.type}\n{ss.description}";
    }
    
    private static Vector2 axis => new Vector2(0, -MainCamera.ScreenHeight);

    public void SetRelative(int thisIndex, int selectedIndex, bool first) {
        var myLoc = axis * (thisIndex - selectedIndex);
        var isSel = thisIndex == selectedIndex;
        var scale = isSel ? selectedScale : unselectedScale;
        canceller?.Cancel();
        canceller = new Cancellable();
        float time = 0.6f;
        if (first) {
            tr.localPosition = myLoc;
            tr.localScale = new Vector3(scale, scale, scale);
        } else {
            RunDroppableRIEnumerator(GoTo(myLoc, time, M.EOutSine, canceller));
            RunDroppableRIEnumerator(ScaleTo(scale, time, M.EOutSine, canceller));
        }
    }
}