using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;

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
}