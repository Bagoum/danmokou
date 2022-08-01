using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Danmokou.Core;
using Danmokou.Graphics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using InternalSetNativeData = System.Action<UnityEngine.ComputeBuffer, System.IntPtr, int, int, int, int>;

namespace Danmokou.Graphics {
public static class MaterialUtils {
    /// <summary>
    /// Assumes the color is premultiplied. Color blending only (alpha is always One OneMinusSrcAlpha)
    /// </summary>
    public static (BlendMode src, BlendMode dst, BlendOp op) ToBlendVars(this DRenderMode rm) {
        if (rm == DRenderMode.NORMAL) {
            return (BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendOp.Add);
        } else if (rm == DRenderMode.ADDITIVE) {
            return (BlendMode.One, BlendMode.One, BlendOp.Add);
        } else if (rm == DRenderMode.SOFT_ADDITIVE) {
            return (BlendMode.OneMinusDstColor, BlendMode.One, BlendOp.Add);
        } else if (rm == DRenderMode.NEGATIVE) {
            return (BlendMode.One, BlendMode.One, BlendOp.ReverseSubtract);
        }
        throw new Exception($"Can't handle render mode {rm}");
    }

    public static void SetBlendMode(Material mat, DRenderMode rm) {
        var (src, dst, op) = rm.ToBlendVars();
        mat.SetFloat(PropConsts.blendSrcMethod, (int) src);
        mat.SetFloat(PropConsts.blendDstMethod, (int) dst);
        mat.SetFloat(PropConsts.blendOp, (int) op);
    }

    public static void SetBlendMode(this MaterialPropertyBlock pb, DRenderMode rm) {
        var (src, dst, op) = rm.ToBlendVars();
        pb.SetFloat(PropConsts.blendSrcMethod, (int) src);
        pb.SetFloat(PropConsts.blendDstMethod, (int) dst);
        pb.SetFloat(PropConsts.blendOp, (int) op);
    }

    public static void SetBufferFromArray(this MaterialPropertyBlock pb, int id, ComputeBufferPool cbp, Array data, int ct) {
        var cb = cbp.Rent();
        cb.SetData(data, 0, 0, ct);
        pb.SetBuffer(id, cb);
    }
    
    private static readonly InternalSetNativeData SetNativeData = (InternalSetNativeData)
        Delegate.CreateDelegate(typeof(InternalSetNativeData),
            typeof(ComputeBuffer).GetMethod("InternalSetNativeData",
                BindingFlags.NonPublic | BindingFlags.Instance)!);
    
    public static void SetBufferFromNativeArray(this MaterialPropertyBlock pb, int id, ComputeBufferPool cbp, IntPtr data, int ct, int stride) {
        var cb = cbp.Rent();
        SetNativeData(cb, data, 0, 0, ct, stride);
        pb.SetBuffer(id, cb);
    }
    public static void SetBufferFromNativeArray<T>(this MaterialPropertyBlock pb, int id, ComputeBufferPool cbp, NativeArray<T> data, int ct) where T : struct {
        var cb = cbp.Rent();
        cb.SetData(data, 0, 0, ct);
        pb.SetBuffer(id, cb);
    }
}
}
