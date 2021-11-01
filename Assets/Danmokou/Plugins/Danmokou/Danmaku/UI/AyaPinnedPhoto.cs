using System.Collections;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.UI {
/// <summary>
/// Class describing a visual representation of an AyaPhoto.
/// <br />Note: these objects update during pause.
/// </summary>
public class AyaPinnedPhoto : CoroutineRegularUpdater {
    public override EngineState UpdateDuring => EngineState.EFFECT_PAUSE;

    private Transform tr = null!;
    private SpriteRenderer sr = null!;
    private MaterialPropertyBlock pb = null!;
    public float timeToPosition;
    public float successRotationLoops = 3;
    public float timeToFallOff;
    public float fallVelocity = -3f;
    public float fallRotationSpeed = -240f;
    public float desiredSize = 2f;
    private Vector2 loc;
    private void Awake() {
        tr = transform;
        sr = GetComponent<SpriteRenderer>();
        sr.GetPropertyBlock(pb = new MaterialPropertyBlock());
        sr.enabled = false;
    }

    public void SetSize(AyaPhoto photo, float size) {
        float overscale = size / Mathf.Max(photo.ScreenHeight, photo.ScreenWidth);
        tr.localScale = new Vector3(overscale, overscale, overscale);
    }
    public void Initialize(AyaPhoto photo, Vector2 source, Vector2? targetLocation) {
        if (InitializeAt(photo, source)) {
            RunDroppableRIEnumerator(targetLocation.Try(out var target) ?
                GoToPosition(photo, source, target) :
                FallOff(photo));
        }
    }

    public bool InitializeAt(AyaPhoto photo, Vector2 point) {
        SetSize(photo, desiredSize);
        tr.position = loc = point;
        tr.eulerAngles = new Vector3(0, 0, photo.Angle);
        if (photo.TryLoad(out Sprite? s)) {
            sr.sprite = s;
            sr.GetPropertyBlock(pb);
            // ReSharper disable once Unity.PreferAddressByIdToGraphicsParams
            pb.SetFloat("_PhotoPPU", photo.PPU);
            sr.SetPropertyBlock(pb);
            sr.enabled = true;
            return true;
        } else {
            Destroy(gameObject);
            return false;
        }
    }
    
    private static float EndRotAdjust() => RNG.GetFloatOffFrame(-0.05f, 0.05f);

    private IEnumerator GoToPosition(AyaPhoto photo, Vector2 source, Vector2 target) {
        float ea0 = photo.Angle;
        float endRotAdjust = EndRotAdjust();
        for (float t = 0; t < timeToPosition; t += ETime.FRAME_TIME) {
            tr.position = loc = Vector2.Lerp(source, target, M.EOutSine(t / timeToPosition));
            tr.eulerAngles = new Vector3(0, 0, ea0 + 360 *
                Mathf.Lerp(-successRotationLoops, endRotAdjust, M.EOutSine(t / timeToPosition)));
            yield return null;
        }
        tr.position = loc = target;
        tr.eulerAngles = new Vector3(0, 0, ea0 + 360 * endRotAdjust);
    }

    private IEnumerator FallOff(AyaPhoto photo) {
        Color bc = sr.color;
        Color c = bc;
        float ea0 = photo.Angle;
        for (float t = 0; t < timeToFallOff; t += ETime.FRAME_TIME) {
            tr.position = loc += new Vector2(0, fallVelocity * ETime.FRAME_TIME);
            tr.eulerAngles = new Vector3(0, 0, ea0 += fallRotationSpeed * ETime.FRAME_TIME);
            c.a = bc.a * (1 - t / timeToFallOff);
            sr.color = c;
            yield return null;
        }
        c.a = 0;
        sr.color = c;
        Destroy(gameObject);
    }
}
}
