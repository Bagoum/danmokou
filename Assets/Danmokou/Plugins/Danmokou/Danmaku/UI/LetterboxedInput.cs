using Danmokou.Core;
using Danmokou.Services;
using Danmokou.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// This class handles remapping mouse positions to support DMK's letterboxing functionality (see FinalScreenRender.shader and the UITK workaround in <see cref="UIBuilderRenderer.RemakeTexture"/>).
/// <br/>- The internal resolution (used in the RenderTextures) of the game may differ from its output resolution;
/// <br/>- The output resolution may have a different aspect ratio from the internal resolution.
/// </summary>
public class LetterboxedInput : BaseInput {
    protected override void Awake() {
        base.Awake();
        GetComponent<StandaloneInputModule>().inputOverride = this;
    }

    public override Touch GetTouch(int index) {
        var t = base.GetTouch(index);
        t.position = RescalePosition(t.position);
        t.rawPosition = RescalePosition(t.rawPosition);
        return t;
    }

    public override Vector2 mousePosition => RescalePosition(base.mousePosition);

    /// <summary>
    /// Convert screen pixel coordinates to the coordinate system of the main RenderTextures.
    /// </summary>
    /// <param name="trueScreenLoc"></param>
    /// <returns></returns>
    private Vector2 RescalePosition(Vector2 trueScreenLoc) {
        var raw = trueScreenLoc;
        var (screenW, screenH) = (Screen.width, Screen.height);
        var screenAspect = screenW / (float)screenH;
        var (internW, internH) = (DMKMainCamera.RenderTo.width, DMKMainCamera.RenderTo.height);
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
        //Logs.Log($"{trueScreenLoc} -> {raw * scale}");
        return raw * scale;
    }
}