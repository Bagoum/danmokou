using DMK.DMath;
using DMK.Reflection;
using JetBrains.Annotations;
using UnityEngine;


namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Ending Config")]
public class EndingConfig : ScriptableObject {
    public string key;
    public string dialogueKey;
    public string predicate;

    public bool Matches => predicate.Into<Pred>()(ParametricInfo.Zero);
}
}