using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.UI;
using UnityEngine;
// ReSharper disable Unity.PreferAddressByIdToGraphicsParams

namespace Danmokou.Services {

public interface IPlayScreenShader {
    IDisposable AddXRotation(float dx, float t);
    IDisposable AddYRotation(float dy, float t);
    void ShowBlackHole(BlackHoleEffect bhe);
    void ShowPixelation(Pixelation pix);
}

public readonly struct Pixelation {
    public readonly float time;
    public readonly TP center;
    public readonly BPY radius;
    public readonly BPY? xBlocks;
    public readonly bool pixelize;
    
    public Pixelation(float time, TP center, BPY radius, BPY? xBlocks, bool pixelize = true) {
        this.time = time;
        this.center = center;
        this.radius = radius;
        this.xBlocks = xBlocks;
        this.pixelize = pixelize;
    }
}
public readonly struct BlackHoleEffect {
    public readonly float absorbT;
    public readonly float hideT;
    public readonly float fadeBackT;
    public BlackHoleEffect(float absorbT, float hideT, float fadeBackT) {
        this.absorbT = absorbT;
        this.hideT = hideT;
        this.fadeBackT = fadeBackT;
    }
}
public class PlayScreenPostprocessing : CoroutineRegularUpdater, IPlayScreenShader {
    private static readonly int rotX = Shader.PropertyToID("_RotateX");
    private static readonly int rotY = Shader.PropertyToID("_RotateY");
    private static readonly int rotZ = Shader.PropertyToID("_RotateZ");
    private static readonly int blackHoleT = Shader.PropertyToID("_BlackHoleT");
    private static readonly int screenRect = Shader.PropertyToID("_ScreenRectBound");
    

    private readonly DisturbedSum<float> targetXRot = new(0f);
    private readonly DisturbedSum<float> targetYRot = new(0);
    private float sourceXRot = 0f;
    private float sourceYRot = 0f;
    private float timeToTarget = 0f;
    private float timeElapsedToTarget = 0f;

    private float lastXRot = 0f;
    private float lastYRot = 0f;

    private Cancellable? pixelizeOnCt;
    private Cancellable? pixelizeOffCt;

    public Shader seijaShader = null!;
    private Material seijaMaterial = null!;

    private void Awake() {
        seijaMaterial = new Material(seijaShader);
        seijaMaterial.SetVector(screenRect, new(0.5f, 0.5f, 0.5f, 0.5f));
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IPlayScreenShader>(this);

        Listen(targetXRot, _ => UndoAddition());
        Listen(targetYRot, _ => UndoAddition());
    }

    public override void FirstFrame() {
        UpdateBounds();
        SetLocation(0, 0);
        //Wait until after UICamera.targetTexture is set,
        // otherwise ToScreenPoint will be relative to the whole screen and not the 16x9 draw area.
        RunDroppableRIEnumerator(RUWaitingUtils.WaitFor(0.1f, Cancellable.Null, UpdateBounds));
    }

    private void UpdateBounds() {
        //Cut off the rendering just outside of the play area in order to avoid artifacting
        if (GameManagement.Instance.InstanceActiveGuardInScene) {
            var bounds = LocationHelpers.PlayableBounds;
            var screenCenter = UIBuilderRenderer.UICamInfo.ToScreenPoint(Vector2.zero);
            var screenHalfDim = -screenCenter +
                                UIBuilderRenderer.UICamInfo.ToScreenPoint(new Vector2(bounds.right + 0.5f,
                                    bounds.top + 0.5f));
            seijaMaterial.SetVector(screenRect, new(screenCenter.x, screenCenter.y, screenHalfDim.x, screenHalfDim.y));
        }
    }

    private void UndoAddition() {
        if (timeElapsedToTarget >= timeToTarget)
            SendLastToSource(1f);
        else
            SendLastToSource(Math.Max(1f, timeToTarget - timeElapsedToTarget));
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (timeElapsedToTarget >= timeToTarget) {
            lastXRot = targetXRot;
            lastYRot = targetYRot;
            SetLocation(targetXRot, targetYRot);
        } else {
            lastXRot = M.Lerp(sourceXRot, targetXRot, timeElapsedToTarget / timeToTarget);
            lastYRot = M.Lerp(sourceYRot, targetYRot, timeElapsedToTarget / timeToTarget);
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

    public IDisposable AddXRotation(float dx, float time) {
        var disp = targetXRot.AddConst(dx);
        SendLastToSource(time);
        return disp;
    }

    public IDisposable AddYRotation(float dy, float time) {
        var disp = targetYRot.AddConst(dy);
        SendLastToSource(time);
        return disp;
    }


    private void SetLocation(float xrd, float yrd) {
        seijaMaterial.SetFloat(rotX, xrd * BMath.degRad);
        seijaMaterial.SetFloat(rotY, yrd * BMath.degRad);
    }

    public void ShowBlackHole(BlackHoleEffect bhe) {
        //Since the seijaMaterial is reinstantiated every time the UI is recreated, we don't need to worry about
        // carryover
        RunDroppableRIEnumerator(BlackHole(bhe));
    }

    private IEnumerator BlackHole(BlackHoleEffect bhe) {
        seijaMaterial.EnableKeyword("FT_BLACKHOLE");
        seijaMaterial.SetFloat("_BlackHoleAbsorbT", bhe.absorbT);
        seijaMaterial.SetFloat("_BlackHoleBlackT", bhe.hideT);
        seijaMaterial.SetFloat("_BlackHoleFadeT", bhe.fadeBackT);
        float t = 0;
        for (; t < bhe.absorbT + bhe.hideT + bhe.fadeBackT; t += ETime.FRAME_TIME) {
            seijaMaterial.SetFloat(blackHoleT, t);
            yield return null;
        }
        seijaMaterial.DisableKeyword("FT_BLACKHOLE");
    }

    public void ShowPixelation(Pixelation pix) {
        if (!pix.pixelize && !seijaMaterial.IsKeywordEnabled("FT_PIXELIZE"))
            return;
        pixelizeOffCt?.Cancel();
        var cT = pix.pixelize ?
            Cancellable.Replace(ref pixelizeOnCt) :
            (pixelizeOnCt = new());
        RunDroppableRIEnumerator(Pixelize(pix, cT));
    }

    private IEnumerator Pixelize(Pixelation pix, ICancellee cT) {
        seijaMaterial.EnableKeyword("FT_PIXELIZE");
        if (pix.pixelize)
            seijaMaterial.SetFloat("_PixelizeRI", -1);
        var pi = ParametricInfo.Zero;
        for (var t = 0f; t < pix.time; t += ETime.FRAME_TIME) {
            if (cT.Cancelled) break;
            pi.t = t;
            if (pix.xBlocks != null)
                seijaMaterial.SetFloat("_PixelizeX", pix.xBlocks(pi));
            //radius is in screen Y units, center is in screen units
            seijaMaterial.SetFloat(pix.pixelize ? "_PixelizeRO" : "_PixelizeRI", 
                MainCamera.MCamInfo.RescaleDims(new(0, pix.radius(pi)), (1,1)).y);
            var center = MainCamera.MCamInfo.ToScreenPoint(pix.center(pi));
            seijaMaterial.SetVector("_PixelizeCenter", new(center.x, center.y, 0, 0));
            yield return null;
        }
        if (!cT.Cancelled && !pix.pixelize)
            seijaMaterial.DisableKeyword("FT_PIXELIZE");
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        //Dest is dirty, rendering to it directly can cause issues if there are alpha pixels.
        //However, SeijaCamera shader uses One Zero, so we don't need to explicitly clear.
        //dest.GLClear();
        UnityEngine.Graphics.Blit(src, dest, seijaMaterial);
    }

    [ContextMenu("YRot")]
    public void debugYRot() => _ = AddYRotation(60, 2f);
    
    [ContextMenu("Black hole")]
    public void debugBlackHole() => ShowBlackHole(new BlackHoleEffect(5, 1, 2));

    [ContextMenu("Pixelize")]
    public void debugPixelize() => ShowPixelation(new(5, _ => LocationHelpers.TruePlayerLocation, 
        pi => 2 * pi.t, null, true));
    
    [ContextMenu("UnPixelize")]
    public void debugUnPixelize() => ShowPixelation(new(5, _ => LocationHelpers.TruePlayerLocation, 
        pi => 2 * pi.t, null, false));

}
}