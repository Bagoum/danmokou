using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using UnityEngine;

namespace Danmokou.Services {
public class SeijaCamera : RegularUpdater {
    private static readonly int rotX = Shader.PropertyToID("_RotateX");
    private static readonly int rotY = Shader.PropertyToID("_RotateY");
    private static readonly int rotZ = Shader.PropertyToID("_RotateZ");
    private static readonly int xBound = Shader.PropertyToID("_XBound");


    private Camera cam = null!;

    private float targetXRot = 0f;
    private float targetYRot = 0f;
    private float sourceXRot = 0f;
    private float sourceYRot = 0f;
    private float timeToTarget = 0f;
    private float timeElapsedToTarget = 0f;

    private float lastXRot = 0f;
    private float lastYRot = 0f;


    public Shader seijaShader = null!;
    private Material seijaMaterial = null!;

    private void Awake() {
        cam = GetComponent<Camera>();
        seijaMaterial = new Material(seijaShader);
        seijaMaterial.SetFloat(xBound, GameManagement.References.bounds.right + 1);
        SetLocation(0, 0);
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
        } else {
            lastXRot = Mathf.Lerp(sourceXRot, targetXRot, timeElapsedToTarget / timeToTarget);
            lastYRot = Mathf.Lerp(sourceYRot, targetYRot, timeElapsedToTarget / timeToTarget);
            SetLocation(lastXRot, lastYRot);
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
        seijaMaterial.SetFloat(rotX, xrd * M.degRad);
        seijaMaterial.SetFloat(rotY, yrd * M.degRad);
    }

    private void OnPreRender() {
        cam.targetTexture = MainCamera.RenderTo;
    }
    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        //Dest is dirty, rendering to it directly can cause issues if there are alpha pixels.
        dest.GLClear();
        UnityEngine.Graphics.Blit(src, dest, seijaMaterial);
    }

}
}