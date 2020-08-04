using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class setparenttest : MonoBehaviour {
    public Transform newParent;

    void Start() {
        transform.SetParent(newParent, false);
    }

    // Update is called once per frame
    void Update() { }
}