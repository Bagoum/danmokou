using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku;
using UnityEngine;
using UnityEngine.Rendering;

public static class MaterialUtils {
    public static (int src, int dst, BlendOp op) ToBlendVars(this RenderMode rm) {
        if (rm == RenderMode.NORMAL) {
            //SrcAlpha OneMinusSrcAlpha
            return (5, 10, BlendOp.Add);
        } else if (rm == RenderMode.ADDITIVE) {
            //SrcAlpha One
            return (5, 1, BlendOp.Add);
        } else if (rm == RenderMode.NEGATIVE) {
            //SrcAlpha One, Negative
            return (5, 1, BlendOp.ReverseSubtract);
        }
        throw new Exception($"Can't handle render mode {rm}");
    }
    public static void SetBlendMode(Material mat, RenderMode rm) {
        var (src, dst, op) = rm.ToBlendVars();
        mat.SetFloat(PropConsts.blendSrcMethod, src);
        mat.SetFloat(PropConsts.blendDstMethod, dst);
        mat.SetFloat(PropConsts.blendOp, (int)op);
    }
    public static void SetBlendMode(this MaterialPropertyBlock pb, RenderMode rm) {
        var (src, dst, op) = rm.ToBlendVars();
        pb.SetFloat(PropConsts.blendSrcMethod, src);
        pb.SetFloat(PropConsts.blendDstMethod, dst);
        pb.SetFloat(PropConsts.blendOp, (int)op);
    }

}
