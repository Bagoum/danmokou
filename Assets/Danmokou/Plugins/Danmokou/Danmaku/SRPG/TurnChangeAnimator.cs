using System;
using System.Collections;
using BagoumLib.Cancellation;
using BagoumLib.Functional;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Graphics;
using Danmokou.Reflection;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
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
    public float endWaitTime = 2f;
    public SFXConfig? defaultSFX;
    public TurnChangeCutin fromFaction = null!;
    public TurnChangeCutin toFaction = null!;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        sr.GetPropertyBlock(pb = new());
    }

    public void Initialize(TurnChangeRequest req) {
        if (req.NextOnLeft) {
            fromFaction.beh.ExternalSetLocalPosition(fromFaction.beh.Location.PtMul(new(-1,1)));
            toFaction.beh.ExternalSetLocalPosition(toFaction.beh.Location.PtMul(new(-1,1)));
            /*var midp = (fromFaction.beh.Location + toFaction.beh.Location) / 2f;
            fromFaction.beh.ExternalSetLocalPosition(midp + (fromFaction.beh.Location-midp).PtMul(new(-1,1)));
            toFaction.beh.ExternalSetLocalPosition(midp + (toFaction.beh.Location-midp).PtMul(new(-1,1)));*/
        }
        if (req.Prev is null)
            Destroy(fromFaction.gameObject);
        else 
            fromFaction.Initialize(req, req.Prev);
        toFaction.Initialize(req, req.Next);
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
        for (t -= outLinesTime; t < endWaitTime && !req.CT.Cancelled; t += ETime.FRAME_TIME) {
            yield return null;
        }
        Destroy(gameObject);
    }
    
}
}