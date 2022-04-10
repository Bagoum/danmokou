﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;
using Danmokou.Graphics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Danmokou.Graphics {
public static class MaterialUtils {
    public static (int dst, BlendOp op) ToBlendVars(this DRenderMode rm) {
        if (rm == DRenderMode.NORMAL) {
            //OneMinusSrcAlpha
            return (10, BlendOp.Add);
        } else if (rm == DRenderMode.ADDITIVE) {
            //One
            return (1, BlendOp.Add);
        } else if (rm == DRenderMode.NEGATIVE) {
            //One, Negative
            return (1, BlendOp.ReverseSubtract);
        }
        throw new Exception($"Can't handle render mode {rm}");
    }

    public static void SetBlendMode(Material mat, DRenderMode rm) {
        var (dst, op) = rm.ToBlendVars();
        mat.SetFloat(PropConsts.blendDstMethod, dst);
        mat.SetFloat(PropConsts.blendOp, (int) op);
    }

    public static void SetBlendMode(this MaterialPropertyBlock pb, DRenderMode rm) {
        var (dst, op) = rm.ToBlendVars();
        pb.SetFloat(PropConsts.blendDstMethod, dst);
        pb.SetFloat(PropConsts.blendOp, (int) op);
    }

}
}
