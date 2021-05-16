using System;
using UnityEngine;

namespace Danmokou.Danmaku.Descriptors {
public class DebugColliderInfo : MonoBehaviour {
    private GenericColliderInfo coll = null!;

    private void Awake() {
        coll = GetComponent<GenericColliderInfo>();
    }

    #if UNITY_EDITOR
    void Update() => coll.DoLiveCollisionTest();
    
    #endif
}
}