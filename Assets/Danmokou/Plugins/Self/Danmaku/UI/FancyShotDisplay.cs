using DMK.Core;
using DMK.DMath;
using DMK.Graphics;
using DMK.Scriptables;
using DMK.Services;
using TMPro;
using UnityEngine;

namespace DMK.UI {
public class FancyShotDisplay : FancyDisplay {
    public TextMeshPro shotTitle = null!;
    public TextMeshPro shotDescription = null!;

    public void SetShot(PlayerConfig p, int index, ShotConfig s, Subshot sub) {
        shotTitle.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        shotDescription.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        var type = s.isMultiShot ? $"Variant {sub.Describe()}" : $"Type {index.ToABC()}";
        var ss = s.GetSubshot(sub);
        shotTitle.text = s.isMultiShot ? $"Multishot:\n{ss.Title}" : ss.Title.ValueOrEn;
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
            RunDroppableRIEnumerator(tr.GoTo(myLoc, time, M.EOutSine, canceller));
            RunDroppableRIEnumerator(tr.ScaleTo(scale, time, M.EOutSine, canceller));
        }
    }
}
}