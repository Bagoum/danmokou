using Danmaku;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;


[CreateAssetMenu(menuName = "Data/Ending Config")]
public class EndingConfig : ScriptableObject {
    public string key;
    public string dialogueKey;
    public string predicate;

    public bool Matches => predicate.Into<Pred>()(GlobalBEH.Main.rBPI);
}