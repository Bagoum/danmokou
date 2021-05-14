using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Utilities {
/// <summary>
/// Use this on main menus to frontload the reflection of some objects.
/// </summary>
public class Preloader : MonoBehaviour {
    public StaticReplay[] replays = null!;
    public TextAsset[] stateMachines = null!;
    [ReflectInto(typeof(FXY))]
    public string[] reflectFXY = null!;
    [ReflectInto(typeof(TP3))]
    public string[] reflectTP3 = null!;

    private void Awake() {
        foreach (var r in replays)
            r.Frames();
        foreach (var sm in stateMachines)
            StateMachineManager.FromText(sm);
        foreach (var s in reflectFXY)
            ReflWrap<FXY>.Wrap(s);
        foreach (var s in reflectTP3)
            ReflWrap<TP3>.Wrap(s);
    }
}
}