using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.Graphics;
using UnityEngine;

public class SetT : MonoBehaviour {
    private SpriteRenderer sr = null!;
    private float t;
    private MaterialPropertyBlock pb = null!;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        sr.GetPropertyBlock(pb = new MaterialPropertyBlock());
    }

    void Update() {
        pb.SetFloat(PropConsts.time, t += ETime.FRAME_TIME);
    }
}