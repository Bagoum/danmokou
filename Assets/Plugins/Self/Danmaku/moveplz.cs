using System.Collections;
using System.Collections.Generic;
using DMath;
using UnityEngine;

public class moveplz : MonoBehaviour {
    private Transform tr;
    private Vector2 pos;
    public string velocity;
    private TP vel;
    private ParametricInfo bpi = new ParametricInfo();

    void Start() {
        tr = transform;
        pos = tr.localPosition;
        vel = velocity.Into<TP>();
    }

    // Update is called once per frame
    void Update() {
        bpi.t += Time.deltaTime;
        pos += Time.deltaTime * vel(bpi);
        tr.localPosition = pos;
    }
}