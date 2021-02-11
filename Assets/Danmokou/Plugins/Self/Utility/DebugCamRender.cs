using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DebugCamRender : MonoBehaviour {
    public string mname = null!;

    void OnPreRender() {
        Debug.Log($"Prerender {mname}");
    }

    void OnPostRender() {
        Debug.Log($"Postrender {mname}");
    }
}