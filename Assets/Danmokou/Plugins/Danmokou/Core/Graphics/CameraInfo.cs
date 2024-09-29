using Danmokou.DMath;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine;

namespace Danmokou.Core {

public class CameraInfo {
    public Camera Camera { get; }
    public float VertRadius { get; private set; }
    public float HorizRadius { get; private set; }
    public float Aspect => HorizRadius / VertRadius;
    public float ScreenWidth => HorizRadius * 2;
    public float ScreenHeight => VertRadius * 2;
    public Vector2 HalfDim => new(HorizRadius, VertRadius);
    public Vector2 Center { get; private set; }
    public CRect Area => new(Center.x, Center.y, HorizRadius, VertRadius, 0);
    private Transform tr;
    private (int, int) lastRes;

    public CameraInfo(Camera camera, Transform transform) {
        Camera = camera;
        VertRadius = camera.orthographicSize;
        UpdateAspectFields((16, 9));
        Center = (tr = transform).position;
    }

    public void UpdateAspectFields((int w, int h) res) {
        HorizRadius = VertRadius * (res.w / (float)res.h);
        lastRes = res;
    }

    public void Recheck() {
        var ortho = Camera.orthographicSize;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (ortho != VertRadius) {
            VertRadius = ortho;
            UpdateAspectFields(lastRes);
        }
        Center = tr.position;
    }
    
    private Vector2 RescaleDims(Vector2 worldDim, (int w, int h) targetResolution) =>
        new(worldDim.x / ScreenWidth * targetResolution.w, worldDim.y / ScreenHeight * targetResolution.h);

    /// <inheritdoc cref="ToXMLDims"/>
    public Vector2 RectToXMLDims(Vector3 minWorldPos, Vector3 maxWorldPos) =>
        (ToXMLPos(maxWorldPos) - ToXMLPos(minWorldPos)).InvertY();
    
    /// <summary>
    /// Relative to this camera, convert world dimensions into screen dimensions in XML coordinates.
    /// </summary>
    public Vector2 ToXMLDims(Vector3 worldPos, Vector3 worldDims) =>
        (ToXMLPos(worldPos + worldDims / 2f) - ToXMLPos(worldPos - worldDims / 2f)).InvertY();

    /// <summary>
    /// Relative to this camera, convert a world position into a screen position in XML coordinates.
    /// </summary>
    public Vector2 ToXMLPos(Vector3 worldPos) {
        var viewpoint = Camera.WorldToViewportPoint(worldPos);
        var viewrect = Camera.rect;
        var screenpoint = viewrect.min + (Vector2)viewpoint.MulBy(viewrect.size);
        return new(UIBuilderRenderer.UIResolution.w * screenpoint.x,
            UIBuilderRenderer.UIResolution.h * (1 - screenpoint.y));
    }
}

}