using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Day Configuration")]
public class DayConfig : ScriptableObject {
    public BossConfig[] bosses = null!;
    public string dayTitle = "";
}
}