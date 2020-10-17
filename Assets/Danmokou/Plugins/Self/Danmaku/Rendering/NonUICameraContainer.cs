using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NonUICameraContainer : MonoBehaviour {
    public bool autoShiftCamera;
    private void Awake() {
        if (autoShiftCamera) transform.localPosition = -GameManagement.References.bounds.center;
    }
}