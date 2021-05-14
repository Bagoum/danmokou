using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmokou.UI {
public class VSyncChecker : MonoBehaviour {
    public Color[] colors = null!;
    private SpriteRenderer sr = null!;
    private int colorIs = 0;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update() {
        colorIs = (colorIs + 1) % colors.Length;
        sr.color = colors[colorIs];
    }
}
}