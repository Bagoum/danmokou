﻿using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Day Configuration")]
public class DayConfig : ScriptableObject {
    public BossConfig[] bosses = null!;
    public string dayTitle = "";
}
}