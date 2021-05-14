using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using JetBrains.Annotations;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Ending Config")]
public class EndingConfig : ScriptableObject {
    public string key = "";
    public string dialogueKey = "";
    [ReflectInto(typeof(Pred))]
    public string predicate = "";

    public bool Matches => predicate.Into<Pred>()(ParametricInfo.Zero);
}
}