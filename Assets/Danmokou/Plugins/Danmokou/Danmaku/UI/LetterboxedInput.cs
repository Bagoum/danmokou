using Danmokou.Services;
using Danmokou.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// This class handles remapping mouse positions to support DMK's letterboxing functionality (see FinalScreenRender.shader and the UITK workaround in <see cref="UIBuilderRenderer.RemakeTexture"/>).
/// <br/>- The internal resolution (used in the RenderTextures) of the game may differ from its output resolution;
/// <br/>- The output resolution may have a different aspect ratio from the internal resolution.
/// <br/>TODO: this solves canvas, what about uitk?
/// </summary>
public class LetterboxedInput : BaseInput {
    protected override void Awake() {
        base.Awake();
        GetComponent<StandaloneInputModule>().inputOverride = this;
    }

    public override Vector2 mousePosition {
        get {
            //Convert screen pixel coordinates to internal resolution coordinates.
            var raw = base.mousePosition;
            var (screenW, screenH) = (Screen.width, Screen.height);
            var screenAspect = screenW / (float)screenH;
            var (internW, internH) = (MainCamera.RenderTo.width, MainCamera.RenderTo.height);
            var internAspect = internW / (float)internH;

            float scale;
            if (screenAspect > internAspect) {
                //Screen is wider, causing pillarboxing. Scale by height
                scale = internH / (float)screenH;
                var clampedScreenW = screenH * internAspect;
                raw.x -= (screenW - clampedScreenW) / 2f;
            } else {
                //Screen is thinner, causing letterboxing. Scale by width
                scale = internW / (float)screenW;
                var clampedScreenH = screenW / internAspect;
                raw.y -= (screenH - clampedScreenH) / 2f;
            }
            //Logs.Log($"{raw * scale}");
            return raw * scale;
        }
    }
}