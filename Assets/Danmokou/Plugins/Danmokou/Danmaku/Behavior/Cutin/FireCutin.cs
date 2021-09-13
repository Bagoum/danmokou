using System;
using System.Collections;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou.Services;
using TMPro;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class FireCutin : BehaviorEntity {
    public SpriteRenderer fireSprite = null!;
    private MaterialPropertyBlock firePB = null!;
    public int xBlocks;
    public int yBlocks;
    public Color color1;
    public Color color2;
    public Color color3;
    public Vector2 fireMultiplier;
    private BPY fireScaler = null!;
    public Vector2 startLoc;
    public string movement = "";
    private TP velMov = null!;
    public float timeToMidSeq;
    public float timeFromMidToFirstHit;
    public float timeFromFirstToSecondHit;
    private float timeToLerpStart => timeToMidSeq + timeFromMidToFirstHit - timeFromFirstToSecondHit;
    private float timeToFirstHit => timeToMidSeq + timeFromMidToFirstHit;
    private float timeToSecondHit => timeToMidSeq + timeFromMidToFirstHit + timeFromFirstToSecondHit;
    private float timeToSecondHitPost => timeToMidSeq + timeFromMidToFirstHit + 2 * timeFromFirstToSecondHit;

    //public TextMeshPro upperText;
    //public TextMeshPro lowerText;
    public Transform upperTr = null!;
    public Transform lowerTr = null!;
    //private Transform upperTr;
    //private Transform lowerTr;
    private Vector3 upperTextBaseLoc;
    private Vector3 lowerTextBaseLoc;
    public Vector3 upperTextOffset;
    public Vector3 lowerTextOffset;
    private TP3 upperTextLerp = null!;
    private TP3 lowerTextLerp = null!;
    public Vector2 textScale;
    private BPY upperTextScaler = null!;
    private BPY lowerTextScaler = null!;
    public SpriteRenderer textBacker = null!;
    public Vector2 textBackFillTime;
    private MaterialPropertyBlock textBackPB = null!;
    private BPY textBackFiller = null!;


    private const float sx = -0.1f;
    private const float sy = 0.2f;
    public Texture2D textBackSprite = null!;

    protected override void Awake() {
        string CXYZ(Vector3 loc) => $"pxyz {loc.x} {loc.y} {loc.z}";
        base.Awake();
        velMov = movement.Into<TP>();
        tr.localPosition = startLoc;
        fireScaler =
            //WARNING incompatible with baking
            FormattableString
                .Invariant(
                    $"lerpsmooth ebounce2 {timeToFirstHit} {timeToSecondHitPost} t {fireMultiplier.x} {fireMultiplier.y}")
                .Into<BPY>();
        upperTextLerp =
            FormattableString
                .Invariant($"lerpsmooth in-sine {timeToLerpStart} {timeToFirstHit} t {CXYZ(upperTextOffset)} zero")
                .Into<TP3>();
        lowerTextLerp =
            FormattableString
                .Invariant($"lerpsmooth in-sine {timeToFirstHit} {timeToSecondHit} t {CXYZ(lowerTextOffset)} zero")
                .Into<TP3>();
        upperTextScaler = FormattableString
            .Invariant($"lerpsmooth out-sine {sx} {sy} (t - {timeToFirstHit}) {textScale.x} {textScale.y}").Into<BPY>();
        lowerTextScaler = FormattableString
            .Invariant($"lerpsmooth out-sine {sx} {sy} (t - {timeToSecondHit}) {textScale.x} {textScale.y}")
            .Into<BPY>();
        if (fireSprite != null) {
            fireSprite.GetPropertyBlock(firePB = new MaterialPropertyBlock());
            firePB.SetFloat(PropConsts.xBlocks, xBlocks);
            firePB.SetFloat(PropConsts.yBlocks, yBlocks);
            firePB.SetColor(PropConsts.color1, color1);
            firePB.SetColor(PropConsts.color2, color2);
            firePB.SetColor(PropConsts.color3, color3);
            firePB.SetFloat(PropConsts.multiplier, fireScaler(bpi));
        }
        //SetColorA(upperText, 0);
        //SetColorA(lowerText, 0);
        //upperTr = upperText.transform;
        //lowerTr = lowerText.transform;
        upperTextBaseLoc = upperTr.localPosition;
        lowerTextBaseLoc = lowerTr.localPosition;
        RunDroppableRIEnumerator(Tracker());

        textBacker.GetPropertyBlock(textBackPB = new MaterialPropertyBlock());
        textBackPB.SetFloat(PropConsts.fillRatio, 0);
        textBackPB.SetTexture(PropConsts.trueTex, textBackSprite);
        textBackFiller = FormattableString.Invariant($"lerpt {textBackFillTime.x} {textBackFillTime.y} 0 1")
            .Into<BPY>();
    }

    private void SetColorA(TextMeshPro t, float a) {
        var c = t.color;
        c.a = a;
        t.color = c;
    }

    private IEnumerator Tracker() {
        for (float t = 0; t < timeToMidSeq; t += ETime.FRAME_TIME) yield return null;
        //SetColorA(upperText, 1);
        //SetColorA(lowerText, 1);
        for (float t = 0; t < timeFromMidToFirstHit; t += ETime.FRAME_TIME) yield return null;
        ServiceLocator.SFXService.Request("x-metal");
        for (float t = 0; t < timeFromFirstToSecondHit; t += ETime.FRAME_TIME) yield return null;
        ServiceLocator.SFXService.Request("x-metal");
    }

    protected override void RegularUpdateRender() {
        tr.localPosition += (Vector3) velMov(bpi) * ETime.FRAME_TIME;
        upperTr.localPosition = upperTextBaseLoc + upperTextLerp(bpi);
        upperTr.localScale = (new Vector3(1, 1, 1)) * upperTextScaler(bpi);
        lowerTr.localPosition = lowerTextBaseLoc + lowerTextLerp(bpi);
        lowerTr.localScale = (new Vector3(1, 1, 1)) * lowerTextScaler(bpi);
        if (fireSprite != null) {
            firePB.SetFloat(PropConsts.multiplier, fireScaler(bpi));
            firePB.SetFloat(PropConsts.time, bpi.t);
            fireSprite.SetPropertyBlock(firePB);
        }
        textBackPB.SetFloat(PropConsts.time, bpi.t);
        textBackPB.SetFloat(PropConsts.fillRatio, textBackFiller(bpi));
        textBacker.SetPropertyBlock(textBackPB);
        base.RegularUpdateRender();
    }
}
}