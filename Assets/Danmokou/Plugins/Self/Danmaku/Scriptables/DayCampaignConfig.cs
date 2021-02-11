using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Day Campaign Configuration")]
public class DayCampaignConfig : ScriptableObject {
    public string key = "";
    public PlayerConfig[] players = null!;
    public DayConfig[] days = null!;
}
}