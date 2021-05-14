using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Danmokou.Graphics {
public static class GraphicsUtils {
    public static readonly (int w, int h) BestResolution = (3840, 2160);
    public static RenderTexture CopyAsTemp(this RenderTexture src) {
        var rt = RenderTexture.GetTemporary(src.descriptor);
        UnityEngine.Graphics.Blit(src, rt);
        return rt;
    }

    public static Texture2D IntoTex(this RenderTexture rt) {
        Texture2D tex = new Texture2D(rt.width, rt.height, rt.graphicsFormat,
            TextureCreationFlags.None);
        var rta = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = (rta == rt) ? null : rta;
        return tex;
    }

    public static void GLClear(this RenderTexture rt) {
        var rta = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rta;

    }

    public static void DestroyTexOrRT(this Texture tex) {
        if (tex is RenderTexture rt)
            rt.Release();
        else
            Object.Destroy(tex);
    }
    
    public static void SetOrUnsetKeyword(this Material mat, bool enable, string keyword) {
        if (enable)
            mat.EnableKeyword(keyword);
        else
            mat.DisableKeyword(keyword);
    }
}
}