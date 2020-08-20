using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;


[CreateAssetMenu(menuName = "Data/Day Campaign Configuration")]
public class DayCampaignConfig : ScriptableObject {
    public string key;
    public DayConfig[] days;
}