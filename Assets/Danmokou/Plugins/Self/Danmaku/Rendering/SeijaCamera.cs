using System;
using System.Collections;
using DMath;
using UnityEngine;

public class SeijaCamera : MonoBehaviour {
    // This can and will be reset every level by a new camera controller
    private static SeijaCamera main;
    private Transform tr;
    private float radius;

    private void Awake() {
        main = this;
        tr = transform;
        radius = tr.localPosition.z;
    }

    [ContextMenu("LR Flip")]
    public void FlipLeftRight() {
        StartCoroutine(IFlipLeftRight("out-sine", 1.5f, false));
    }
    [ContextMenu("UD Flip")]
    public void FlipUpDown() {
        StartCoroutine(IFlipUpDown("out-sine", 1.5f, false));
    }

    public static void FlipX(string ease, float time, bool reverse) {
        main.StartCoroutine(main.IFlipLeftRight(ease, time, reverse));
    }
    public static void FlipY(string ease, float time, bool reverse) {
        main.StartCoroutine(main.IFlipUpDown(ease, time, reverse));
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
        float rxcd = DMath.M.CosDeg(rot.x);
        for (float elapsed = 0; elapsed < time;) {
            yield return null;
            elapsed += ETime.dT;
            rot.y = startRotY + delta * ease(elapsed / time);
            loc.x = DMath.M.SinDeg(rot.y) * radius;
            loc.z = rxcd * DMath.M.CosDeg(rot.y) * radius;
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
        float rycd = DMath.M.CosDeg(rot.y);
        for (float elapsed = 0; elapsed < time;) {
            yield return null;
            elapsed += ETime.dT;
            rot.x = startRotX + delta * ease(elapsed / time);
            loc.y = -DMath.M.SinDeg(rot.x) * radius;
            loc.z = rycd * DMath.M.CosDeg(rot.x) * radius;
            tr.localEulerAngles = rot;
            tr.localPosition = loc;
        }
        tr.localEulerAngles = endRot;
        tr.localPosition = endLoc;
        
    }
    
    
}