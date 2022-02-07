using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmokou.UI {
/// <summary>
/// Class that verifies VSYNC behavior by changing the color of a sprite every frame.
/// <br/>If you set the colors to RED and CYAN, then running the game at 60fps should result in
///  the sprite *appearing* gray.
/// </summary>
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