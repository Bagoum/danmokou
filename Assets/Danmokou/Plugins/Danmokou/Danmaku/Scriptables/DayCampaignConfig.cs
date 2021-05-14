﻿using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Day Campaign Configuration")]
public class DayCampaignConfig : ScriptableObject {
    public string key = "";
    public ShipConfig[] players = null!;
    public DayConfig[] days = null!;
}
}