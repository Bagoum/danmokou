using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RefreshRateWarning : MonoBehaviour {
    private TextMeshPro text;
    public Color normalColor;
    public Color warningColor;

    private static (bool invalid, string msg) GetString() {
        var baseStr =
            $"Your monitor refresh rate is {Screen.currentResolution.refreshRate} Hz. The game will run at {SaveData.s.RefreshRate} Hz.";
        if (Math.Abs(Screen.currentResolution.refreshRate - SaveData.s.RefreshRate) > 2) {
            int ratio = Mathf.RoundToInt((Screen.currentResolution.refreshRate /SaveData.s.RefreshRate  - 1) * 100);
            string error = ratio < 0 ? "slower" : "faster";
            return (true, $"<size=8>WARNING</size>\n{baseStr} ({Math.Abs(ratio)}% {error})" +
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