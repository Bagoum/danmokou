using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace Danmokou.Graphics {
/// <summary>
/// Class to mediate calling URP methods on <see cref="IURPCamera"/>.
/// </summary>
public static class URPCameraManager {
    private static readonly Dictionary<Camera, DeletionMarker<IURPCamera>> mapping = new();
    /// <summary>
    /// Event that captures both <see cref="RenderPipelineManager.beginCameraRendering"/>
    /// and <see cref="Camera.onPreCull"/>.
    /// </summary>
    /// <returns></returns>
    public static IObservable<Camera> OnRenderingStarted => _onRenderingStarted;
    private static readonly Event<Camera> _onRenderingStarted = new();

    public static void SetupCallbacks() {
        RenderPipelineManager.beginContextRendering += (ctx, cams) => {
            //Would prefer to do this on game initialization, but internal SRP
            // will do a delayed overwrite in its first rendering round
            RTHandles.SetReferenceSize(SaveData.s.Resolution.w, SaveData.s.Resolution.h);
            foreach (var cam in cams)
                if (Find(cam) is { } urpC)
                    urpC.BeginContextRendering(ctx);
        };
        RenderPipelineManager.endContextRendering += (ctx, cams) => {
            foreach (var cam in cams)
                if (Find(cam) is { } urpC)
                    urpC.EndContextRendering(ctx);
        };
        RenderPipelineManager.beginCameraRendering += (ctx, cam) => {
            if (Find(cam) is { } urpC)
                urpC.BeginCameraRendering(ctx);
            _onRenderingStarted.OnNext(cam);
        };
        RenderPipelineManager.endCameraRendering += (ctx, cam) => {
            if (Find(cam) is { } urpC)
                urpC.EndCameraRendering(ctx);
        };
        Camera.onPreCull += _onRenderingStarted.OnNext;
    }
    
    
    public static IDisposable Register(Camera c, IURPCamera urpC) {
        return mapping[c] = new DeletionMarker<IURPCamera>(urpC, 0);
    }

    public static IURPCamera? Find(Camera c) =>
        mapping.TryGetValue(c, out var urpC) && !urpC.MarkedForDeletion ?
            urpC.Value :
            null;

    public static void Clear() {
        var ct = 0;
        foreach (var kv in mapping.ToList())
            if (kv.Value.MarkedForDeletion) {
                ++ct;
                mapping.Remove(kv.Key);
            }
        Logs.Log($"Cleared out {ct} deleted cameras");
    }


}
}