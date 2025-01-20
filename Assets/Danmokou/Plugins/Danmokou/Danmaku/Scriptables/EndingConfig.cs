using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using JetBrains.Annotations;
using UnityEngine;


namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Ending Config")]
public class EndingConfig : ScriptableObject {
    public string key = "";
    public TextAsset stateMachine = null!;
    [ReflectInto(typeof(Pred))]
    public string predicate = "";

    public bool Matches => string.IsNullOrWhiteSpace(predicate) ||
                           predicate.Into<Pred>()(ParametricInfo.Zero);
}
}