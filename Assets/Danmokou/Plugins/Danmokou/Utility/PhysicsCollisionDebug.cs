using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using UnityEngine;

public class PhysicsCollisionDebug : CoroutineRegularUpdater {
    public float radius = 0.05f;
    public Vector3 velocity;
    public override void RegularUpdate() {
        var loc = transform.position;
        transform.position = loc + velocity * ETime.FRAME_TIME;
        base.RegularUpdate();
    }

    private void OnTriggerEnter2D(Collider2D other) {
        Logs.Log($"{gameObject.name} received a trigger enter from {other.gameObject.name}");
    }
    private void OnTriggerStay2D(Collider2D other) {
        Logs.Log($"{gameObject.name} received a trigger stay from {other.gameObject.name}");
    }
    private void OnTriggerExit2D(Collider2D other) {
        Logs.Log($"{gameObject.name} received a trigger exit from {other.gameObject.name}");
    }
}