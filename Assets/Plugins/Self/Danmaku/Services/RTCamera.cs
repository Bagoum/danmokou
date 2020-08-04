using System;
using UnityEngine;

public class RTCamera : MonoBehaviour {
    private Camera c;
    private void Awake() {
        c = GetComponent<Camera>();
    }

    /*
    private void OnPreRender() {
        //c.targetTexture = MainCamera.RT;
    }

    private void OnPostRender() {
        //Don't do this, it causes renders to no work???
        //c.targetTexture = null;
    }*/
}
