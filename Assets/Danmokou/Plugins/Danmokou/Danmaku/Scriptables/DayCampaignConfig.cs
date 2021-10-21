using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Day Campaign Configuration")]
public class DayCampaignConfig : ScriptableObject, ICampaignMeta {
    public string key = "";
    public ShipConfig[] players = null!;
    public DayConfig[] days = null!;
    
    public string Key => key;
    public bool Replayable => false;
    public bool AllowDialogueSkip => true;
}
}