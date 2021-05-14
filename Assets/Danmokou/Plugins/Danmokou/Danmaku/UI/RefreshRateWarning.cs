using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public class RefreshRateWarning : MonoBehaviour {
    private TextMeshPro text = null!;
    public Color normalColor;
    public Color warningColor;

    private static (bool invalid, string msg) GetString() {
        var baseStr =
            $"Your monitor refresh rate is {Screen.currentResolution.refreshRate} Hz. The game will run at {SaveData.s.RefreshRate} Hz.";
        if (Math.Abs(Screen.currentResolution.refreshRate - SaveData.s.RefreshRate) > 2) {
            int ratio = Mathf.RoundToInt((Screen.currentResolution.refreshRate / (float) SaveData.s.RefreshRate - 1) *
                                         100);
            string error = ratio < 0 ? "slower" : "faster";
            return (true, $"{baseStr} ({Math.Abs(ratio)}% {error})" +
                          $"\nPlease change your monitor refresh rate to one of 30, 40, 60, 120.");
        } else return (false, baseStr);
    }

    private void Awake() {
        text = GetComponent<TextMeshPro>();
        var (warning, msg) = GetString();
        text.text = msg;
        text.color = (warning) ? warningColor : normalColor;
    }
}
}