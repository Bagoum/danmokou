using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class GraphicsUtils {
    public static RenderTexture CopyAsTemp(this RenderTexture src) {
        var rt = RenderTexture.GetTemporary(src.descriptor);
        Graphics.Blit(src, rt);
        return rt;
    }
    public static RenderTexture CopyInto(this RenderTexture src, [CanBeNull] ref RenderTexture into) {
        if (into == null) into = RenderTexture.GetTemporary(src.descriptor);
        Graphics.Blit(src, into);
        return into;
    }
    public static Texture2D IntoTex(this RenderTexture rt) {
        Texture2D tex = new Texture2D(rt.width, rt.height, rt.graphicsFormat, 
            TextureCreationFlags.None);
        var rta = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = rta;
        return tex;
    }
}