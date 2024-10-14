using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.SRPG {
public record TurnChangeRequest(Faction? Prev, Faction Next, bool NextOnLeft, ICancellee CT, Action Done) {
    public Maybe<SFXConfig?> SFX { get; init; } = Maybe<SFXConfig?>.None;
}

public class TurnChangeAnimator: CoroutineRegularUpdater {
    private SpriteRenderer sr = null!;
    private MaterialPropertyBlock pb = null!;
    public float inLinesTime = 0.7f;
    public float stayTime = 1f;
    public float outLinesTime = 0.7f;
    public SFXConfig? defaultSFX;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        sr.GetPropertyBlock(pb = new());
    }

    public void Initialize(TurnChangeRequest req) {
        RunRIEnumerator(AnimateScreenLines(req));
    }

    private IEnumerator AnimateScreenLines(TurnChangeRequest req) {
        ISFXService.SFXService.Request(req.SFX.Or(defaultSFX));
        pb.SetFloat("_XMult", req.NextOnLeft ? -1 : 1);
        pb.SetFloat(PropConsts.FillMult, 1);
        float t = 0;
        for (; t < inLinesTime && !req.CT.Cancelled; t += ETime.FRAME_TIME) {
            pb.SetFloat(PropConsts.FillRatio, t/inLinesTime);
            sr.SetPropertyBlock(pb);
            yield return null;
        }
        for (t -= inLinesTime; t < stayTime && !req.CT.Cancelled; t += ETime.FRAME_TIME) {
            yield return null;
        }
        pb.SetFloat(PropConsts.FillMult, -1);
        for (t -= stayTime; t < outLinesTime && !req.CT.Cancelled; t += ETime.FRAME_TIME) {
            pb.SetFloat(PropConsts.FillRatio, t/outLinesTime);
            sr.SetPropertyBlock(pb);
            yield return null;
        }
        req.Done();
        Destroy(gameObject);
    }
    
}
}