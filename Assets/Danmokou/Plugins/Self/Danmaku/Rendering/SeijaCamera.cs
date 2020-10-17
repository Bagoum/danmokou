using System;
using System.Collections;
using DMath;
using UnityEngine;

public class SeijaCamera : RegularUpdater {
    // This can and will be reset every level by a new camera controller
    private static SeijaCamera main;
    private Transform tr;
    private float radius;

    private float targetXRot = 0f;
    private float targetYRot = 0f;
    private float sourceXRot = 0f;
    private float sourceYRot = 0f;
    private float timeToTarget = 0f;
    private float timeElapsedToTarget = 0f;

    private float lastXRot = 0f;
    private float lastYRot = 0f;

    private static bool distortionAllowed;
    private static void AllowDistortion() {
        if (!distortionAllowed) {
            distortionAllowed = true;
            Shader.EnableKeyword("ALLOW_DISTORTION");
        }
    }
    private static void DisableDistortion() {
        if (distortionAllowed) {
            distortionAllowed = false;
            Shader.DisableKeyword("ALLOW_DISTORTION");
        }
    }
    private void Awake() {
        main = this;
        tr = transform;
        radius = tr.localPosition.z;
        distortionAllowed = false;
        AllowDistortion();
    }

    public override void RegularUpdate() {
        if (timeElapsedToTarget >= timeToTarget) {
            targetXRot = lastXRot = M.Mod(360, targetXRot);
            targetYRot = lastYRot = M.Mod(360, targetYRot);
            SetLocation(targetXRot, targetYRot);
            if (Math.Max(Math.Abs(targetYRot), Math.Abs(targetXRot)) < 0.00001) AllowDistortion();
        } else {
            lastXRot = Mathf.Lerp(sourceXRot, targetXRot, timeElapsedToTarget / timeToTarget);
            lastYRot = Mathf.Lerp(sourceYRot, targetYRot, timeElapsedToTarget / timeToTarget);
            SetLocation(lastXRot, lastYRot);
            DisableDistortion();
        }
        timeElapsedToTarget += ETime.FRAME_TIME;
    }

    private void SendLastToSource(float time) {
        main.timeToTarget = time;
        main.timeElapsedToTarget = 0f;
        sourceXRot = lastXRot;
        sourceYRot = lastYRot;
    }
    public static void AddXRotation(float dx, float time) {
        main.SendLastToSource(time);
        main.targetXRot += dx;
    }
    public static void AddYRotation(float dy, float time) {
        main.SendLastToSource(time);
        main.targetYRot += dy;
    }

    public static void ResetTargetFlip(float time) {
        if (main.targetXRot * main.targetXRot + main.targetYRot * main.targetYRot > 0) {
            main.SendLastToSource(time);
            main.targetXRot = 360 * Mathf.Round(main.targetXRot / 360);
            main.targetYRot = 360 * Mathf.Round(main.targetYRot / 360);
        }
    }

    private void SetLocation(float xrd, float yrd) {
        tr.localEulerAngles = new Vector3(xrd, yrd, 0f);
        var xc = M.CosDeg(xrd);
        tr.localPosition = radius * new Vector3(xc * M.SinDeg(yrd), -M.SinDeg(xrd), xc * M.CosDeg(yrd));
    }


}