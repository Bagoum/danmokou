using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyThis : MonoBehaviour {
    private void Awake() {
        GameObject go = gameObject;
        go.SetActive(false);
        Destroy(go);
    }
}