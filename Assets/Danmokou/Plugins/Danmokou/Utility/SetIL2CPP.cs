using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SetIL2CPP : MonoBehaviour {
#if UNITY_EDITOR
    //Note: this currently does not work due to an IL2CPP problem (https://forum.unity.com/threads/il2cpp-encountered-a-managed-type-which-it-cannot-convert-ahead-of-time.965555/)
    [ContextMenu("Set Recursive Depth")]
    public void SetRecursiveDepth() {
        PlayerSettings.SetAdditionalIl2CppArgs("--maximum-recursive-generic-depth=16");
    }
#endif
}