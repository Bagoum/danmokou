using System;
using System.Collections;
using System.Collections.Generic;
using DMK.Core;
using UnityEngine;

namespace DMK.Services {
public class NonUICameraContainer : MonoBehaviour {
    public bool autoShiftCamera;

    private void Awake() {
        if (autoShiftCamera) transform.localPosition = -GameManagement.References.bounds.center;
    }
}
}