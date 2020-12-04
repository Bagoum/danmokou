using System;
using System.Collections;
using Core;
using DMath;
using UnityEngine;

public class SeijaCamera : RegularUpdater {
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
        tr = transform;
        radius = tr.localPosition.z;
        distortionAllowed = false;
        AllowDistortion();
    }

    protected override void BindListeners() {
        base.BindListeners();
        Listen(RequestXRotation, AddXRotation);
        Listen(RequestYRotation, AddYRotation);
        Listen(Events.ClearPhase, () => ResetTargetFlip(1f));
#if UNITY_EDITOR || ALLOW_RELOAD
        Listen(Events.LocalReset, () => ResetTargetFlip(0.2f));
#endif
    }

    public static readonly Events.IEvent<(float dx, float t)> RequestXRotation = new Events.Event<(float, float)>();
    public static readonly Events.IEvent<(float dy, float t)> RequestYRotation = new Events.Event<(float, float)>();

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
        timeToTarget = time;
        timeElapsedToTarget = 0f;
        sourceXRot = lastXRot;
        sourceYRot = lastYRot;
    }
    public void AddXRotation((float dx, float time) req) {
        SendLastToSource(req.time);
        targetXRot += req.dx;
    }
    public void AddYRotation((float dy, float time) req) {
        SendLastToSource(req.time);
        targetYRot += req.dy;
    }

    public void ResetTargetFlip(float time) {
        if (targetXRot * targetXRot + targetYRot * targetYRot > 0) {
            SendLastToSource(time);
            targetXRot = 360 * Mathf.Round(targetXRot / 360);
            targetYRot = 360 * Mathf.Round(targetYRot / 360);
        }
    }

    private void SetLocation(float xrd, float yrd) {
        tr.localEulerAngles = new Vector3(xrd, yrd, 0f);
        var xc = M.CosDeg(xrd);
        tr.localPosition = radius * new Vector3(xc * M.SinDeg(yrd), -M.SinDeg(xrd), xc * M.CosDeg(yrd));
    }


}