using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using TMPro;
using UnityEngine;
using System.Linq;

namespace Danmokou.UI {
public class RefreshRateWarning : MonoBehaviour {
    private TextMeshPro text = null!;
    public Color normalColor;
    public Color warningColor;

    private static readonly int[] validRefreshes = { 30, 40, 60, 120, 240, 360, 480 };
    private static (bool invalid, string msg) GetString() {
        var refresh = (int)Math.Round(Screen.currentResolution.refreshRateRatio.value);
        Logs.Log($"Refresh rate: {refresh}");
        if (!validRefreshes.Contains(refresh)) {
            return (true, $"Your monitor refresh rate is {refresh} Hz. This may cause visual tearing.\nAn optimal refresh rate is one of 30, 40, 60, or a multiple of 120.");
        } else return (false, "");
    }

    private void Awake() {
        text = GetComponent<TextMeshPro>();
        var (warning, msg) = GetString();
        text.text = msg;
        text.color = (warning) ? warningColor : normalColor;
    }
}
}