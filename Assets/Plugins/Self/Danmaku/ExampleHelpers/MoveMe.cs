using System.Collections;
using System.Collections.Generic;
using Danmaku;
using DMath;
using UnityEngine;

public class MoveMe : RegularUpdater {
    public string location;
    private TP3 locF;
    private Transform tr;

    private void Start() {
        locF = location.Into<TP3>();
        tr = transform;
    }

    public override void RegularUpdate() {
        tr.localPosition = locF(new ParametricInfo(tr.localPosition, 0, 0, BackgroundOrchestrator.Time));
    }
}