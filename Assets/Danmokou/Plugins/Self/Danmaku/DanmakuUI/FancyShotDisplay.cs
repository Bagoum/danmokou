using System.Collections;
using System.Collections.Generic;
using Danmaku;
using TMPro;
using UnityEngine;

public class FancyShotDisplay : MonoBehaviour {
    public TextMeshPro shotTitle;
    public TextMeshPro shotDescription;

    public void Show(bool show) => gameObject.SetActive(show);

    public void SetShot(PlayerConfig p, int index, ShotConfig s, Enums.Subshot sub) {
        shotTitle.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        shotDescription.fontSharedMaterial.SetMaterialOutline(p.uiColor);
        var type = s.isMultiShot ? $"Multishot Variant {sub.Describe()}" : $"Type {index.ToABC()}";
        var ss = s.GetSubshot(sub);
        shotTitle.text = s.isMultiShot ? $"Multishot:\n{ss.title}" : ss.title;
        shotDescription.text = $"{type} / {ss.type}\n{ss.description}";
    }
}