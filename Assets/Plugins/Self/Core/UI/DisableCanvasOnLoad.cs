using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisableCanvasOnLoad : MonoBehaviour {
    private Canvas canv;
    void Start() {
        canv = GetComponent<Canvas>();
        canv.enabled = false;
    }
}