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
        main.SendLastToSource(time);
        main.targetXRot = 360 * Mathf.Round(main.targetXRot / 360);
        main.targetYRot = 360 * Mathf.Round(main.targetYRot / 360);
    }

    private void SetLocation(float xrd, float yrd) {
        tr.localEulerAngles = new Vector3(xrd, yrd, 0f);
        var xc = M.CosDeg(xrd);
        tr.localPosition = radius * new Vector3(xc * M.SinDeg(yrd), -M.SinDeg(xrd), xc * M.CosDeg(yrd));
    }
    //For below: RX,RY,RZ = euler angles. X,Y,Z = position.
    //For my sanity, do not try to compose these, and do not try to rest the camera
    // at any position other than +- r.
    
    private IEnumerator IFlipLeftRight(string easeMethod, float time, bool reverseDir) {
        //Increase RY by 180. Z = R cos RY. X = R sin RY (assuming negative R by default)
        FXY ease = EaseHelpers.GetFuncOrRemoteOrLinear(easeMethod);
        Vector3 rot = tr.localEulerAngles;
        Vector3 loc = tr.localPosition; // this must be (0, 0, +- r)
        float startRotY = rot.y;
        Vector3 endRot = new Vector3(rot.x, (rot.y + 180) % 360, rot.z);
        Vector3 endLoc = new Vector3(0f, 0f, -loc.z);
        float delta = 180f * (reverseDir ? -1 : 1);
        float rxcd = M.CosDeg(rot.x);
        for (float elapsed = 0; elapsed < time;) {
            yield return null;
            elapsed += ETime.dT;
            rot.y = startRotY + delta * ease(elapsed / time);
            loc.x = M.SinDeg(rot.y) * radius;
            loc.z = rxcd * M.CosDeg(rot.y) * radius;
            tr.localEulerAngles = rot;
            tr.localPosition = loc;
        }
        tr.localEulerAngles = endRot;
        tr.localPosition = endLoc;
    }

    private IEnumerator IFlipUpDown(string easeMethod, float time, bool reverseDir) {
        //Increase RX by 180. Z = R cos RY. Y = -R sin RY (assuming negative R by default)
        FXY ease = EaseHelpers.GetFuncOrRemoteOrLinear(easeMethod);
        Vector3 rot = tr.localEulerAngles;
        Vector3 loc = tr.localPosition; // this must be (0, 0, +- r)
        float startRotX = rot.x;
        Vector3 endRot = new Vector3((rot.x + 180) % 360, rot.y, rot.z);
        Vector3 endLoc = new Vector3(0f, 0f, -loc.z);
        float delta = 180f * (reverseDir ? -1 : 1);
        float rycd = M.CosDeg(rot.y);
        for (float elapsed = 0; elapsed < time;) {
            yield return null;
            elapsed += ETime.dT;
            rot.x = startRotX + delta * ease(elapsed / time);
            loc.y = -M.SinDeg(rot.x) * radius;
            loc.z = rycd * M.CosDeg(rot.x) * radius;
            tr.localEulerAngles = rot;
            tr.localPosition = loc;
        }
        tr.localEulerAngles = endRot;
        tr.localPosition = endLoc;
        
    }
    
    
}