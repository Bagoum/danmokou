using UnityEngine.Rendering;

namespace Danmokou.Graphics {
/// <summary>
/// Interface for cameras that can receive URP callbacks, or on which there might be functions run dependent on
///  URP callbacks (eg. functions that would run under Camera.onPreCull in built-in).
/// </summary>
public interface IURPCamera {
    /// <summary>
    /// Called on <see cref="RenderPipelineManager.beginContextRendering"/>
    /// (ie. before any rendering).
    /// </summary>
    public void BeginContextRendering(ScriptableRenderContext ctx) { }
    /// <summary>
    /// Called on <see cref="RenderPipelineManager.endContextRendering"/>
    /// (ie. after all rendering).
    /// </summary>
    public void EndContextRendering(ScriptableRenderContext ctx) { }
    /// <summary>
    /// Called on <see cref="RenderPipelineManager.beginCameraRendering"/>
    /// (ie. right before this camera's rendering pipeline).
    /// </summary>
    public void BeginCameraRendering(ScriptableRenderContext ctx) { }
    /// <summary>
    /// Called on <see cref="RenderPipelineManager.endCameraRendering"/>
    /// (ie. right after this camera's rendering pipeline).
    /// </summary>
    public void EndCameraRendering(ScriptableRenderContext ctx) { }
}
}