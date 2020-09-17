using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;


[CreateAssetMenu(menuName = "Data/Day Configuration")]
public class DayConfig : ScriptableObject {
    public BossConfig[] bosses;
    public string key;
    public string dayTitle;
}