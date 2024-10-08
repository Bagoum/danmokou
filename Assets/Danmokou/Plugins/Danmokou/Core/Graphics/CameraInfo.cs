using System;
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
    
    public Vector2 RescaleDims(Vector2 worldDim, (int w, int h) targetResolution) =>
        new(worldDim.x / ScreenWidth * targetResolution.w, worldDim.y / ScreenHeight * targetResolution.h);

    public CRect ToScreenRect(CRect worldRect, float worldZ) =>
        new(ToScreenPoint(new(worldRect.MinX, worldRect.MinY, worldZ)),
            ToScreenPoint(new(worldRect.MaxX, worldRect.MaxY, worldZ)), worldRect.angle);
    
    /// <summary>
    /// Relative to this camera, convert world dimensions into screen dimensions in XML coordinates.
    /// </summary>
    public Vector2 ToXMLDims(Vector3 worldPos, Vector3 worldDims) =>
        (ToXMLPos(worldPos + worldDims / 2f) - ToXMLPos(worldPos - worldDims / 2f)).InvertY();

    /// <summary>
    /// Relative to this camera, convert a world position into a screen position in XML coordinates.
    /// </summary>
    public Vector2 ToXMLPos(Vector3 worldPos) => 
        UIBuilderRenderer.ScreenpointToXML(ToScreenPoint(worldPos));

    /// <summary>
    /// Relative to this camera, convert a world position into a screen position in UV coordinates.
    /// </summary>
    public Vector2 ToScreenPoint(Vector3 worldPos) {
        var viewpoint = Camera.WorldToViewportPoint(worldPos);
        var viewrect = Camera.rect;
        return viewrect.min + (Vector2)viewpoint.MulBy(viewrect.size);
    }

    /// <summary>
    /// Relative to this camera, convert a viewport position into a world position with the Z coordinate fixed.
    /// <br/>NB: Unity's ViewportToWorldPoint has the same signature, but instead of fixing the Z coordinate,
    ///  it fixes the distance from camera.
    /// </summary>
    public Vector3 ViewportToWorldFixedZ(Vector2 viewportPos, float worldZ) {
        var ray = Camera.ViewportPointToRay(viewportPos);
        if (Math.Abs(ray.direction.z) < M.MAG_ERR)
            throw new Exception("Can't determine world point with fixed Z");
        var t = (worldZ - ray.origin.z) / ray.direction.z;
        return ray.origin + ray.direction * t;
    }
}

}