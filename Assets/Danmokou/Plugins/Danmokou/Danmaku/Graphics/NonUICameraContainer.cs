using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Services {
public class NonUICameraContainer : MonoBehaviour {
    public bool autoShiftCamera;

    private void Awake() {
        if (autoShiftCamera) 
            transform.localPosition = 
                new Vector3(-LocationHelpers.PlayableBounds.center.x, -LocationHelpers.PlayableBounds.center.y, 
                    transform.localPosition.z);
    }
}
}