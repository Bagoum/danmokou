using System;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using TMPro;
using UnityEngine;

namespace Danmokou.UI {
public abstract class Commentator<T> : CoroutineRegularUpdater {
    [Serializable]
    public struct Comment {
        public LocalizedStringReference text;
        public Sprite sprite;
    }

    private void Awake() {
        HideText();
    }

    public SpriteRenderer sprite = null!;
    public PiecewiseAppear appearController = null!;
    public Canvas textCanvas = null!;
    public TextMeshProUGUI text = null!;

    public void Appear() => appearController.Queue(new PiecewiseAppear.AppearRequest(
        PiecewiseAppear.Action.APPEAR, 0.65f, ShowText));
    public void Disappear() => appearController.Queue(new PiecewiseAppear.AppearRequest(
        PiecewiseAppear.Action.DISAPPEAR, 0, HideText));
    public void SetSprite(Sprite s) => sprite.sprite = s;
    public void ShowText() => textCanvas.enabled = true;
    public void HideText() => textCanvas.enabled = false;
    public void SetText(string txt) => text.text = txt;

    public void SetComment(Comment c) {
        SetSprite(c.sprite);
        SetText(c.text.Value.Value);
    }

    public abstract void SetCommentFromValue(T value);
}
}