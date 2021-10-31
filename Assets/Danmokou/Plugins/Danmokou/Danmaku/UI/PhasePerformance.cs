using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using TMPro;
using UnityEngine;

public class PhasePerformance : PiecewiseAppear {
    public SpriteRenderer background = null!;
    public GameObject container = null!;
    public TextMeshPro descriptionText = null!;
    public TextMeshPro performanceText = null!;

    public float starShowDelay = 0.14f;
    public float waitAfterShown = 0.4f;
    public PhasePerformanceStar[] stars = null!;
    public override Bounds Bounds => background.sprite.bounds.MulBy(background.transform.lossyScale);
    public override Vector2 Center => background.transform.position;
    public override (Texture, bool) Texture => (ServiceLocator.Find<IScreenshotter>().Screenshot(
        new CRect(background.transform, background.sprite.bounds),
        new[] {MainCamera.CamType.UI}), true);

    public void Initialize(string description, PhaseCompletion pc) {
        if (pc.phase.Boss == null || pc.CaptureStars == null) {
            Destroy(gameObject);
            return;
        }
        descriptionText.text = description;
        performanceText.text = $"{pc.Performance} <size=3.6>({pc.ElapsedTime:F1}s)</size>";
        background.color = pc.phase.Boss.colors.uiColor;
        Queue(new AppearRequest(AppearAction.APPEAR, 0.9f, () => 
            RunDroppableRIEnumerator(ShowStars(pc.CaptureStars.Value, pc.phase.Boss.colors.uiHPColor, () =>
                Queue(new AppearRequest(AppearAction.DISAPPEAR, 1f, () => Destroy(gameObject)))))));
    }
    
    public override void Hide() {
        container.SetActive(false);
    }

    public override void Show() {
        container.SetActive(true);
    }

    private IEnumerator ShowStars(int score, Color color, Action cb) {
        for (int ii = 0; ii < stars.Length; ++ii) {
            stars[ii].Show(ii < score ? color : (Color?) null);
            for (float t = 0; t < starShowDelay; t += ETime.FRAME_TIME)
                yield return null;
        }
        for (float t = 0; t < waitAfterShown; t += ETime.FRAME_TIME)
            yield return null;
        cb();
    }

}