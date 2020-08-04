using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VSyncChecker : MonoBehaviour {
    public Color[] colors;
    private SpriteRenderer sr;
    private int colorIs = 0;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update() {
        colorIs = (colorIs + 1) % colors.Length;
        sr.color = colors[colorIs];
    }
}