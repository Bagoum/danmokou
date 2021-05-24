using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace Danmokou.Graphics {
public static class FragmentRendering {
    [Serializable]
    public class FragmentConfig {
        public Sprite fragmentSprite = null!;
        public Material fragmentMaterial = null!;
        public float fragmentRadius;
        public float FragmentDiameter => 2f * fragmentRadius;
        public float fragmentSides = 4;
            
            
        private Mesh? mesh;
        public Mesh Mesh {
            get {
                if (mesh == null) mesh = MeshGenerator.RenderInfo.FromSprite(fragmentSprite, FragmentDiameter);
                return mesh;
            }
        }
    }

    public class Fragment {
        public readonly float baseShapeRot;
        public readonly Vector2 uv;
        private readonly Func<float, Vector3>? rotations;
        private readonly BPY? scale;
        private readonly Vector2 baseLocation;
        private readonly TP offset;

        public ParametricInfo bpi;

        public Vector2 Location => baseLocation + offset(bpi);
        public Vector3 Rotation => rotations?.Invoke(bpi.t) ?? Vector3.zero;
        public float Scale => scale?.Invoke(bpi) ?? 1f;
        
        
        public Fragment(Vector2 location, Vector2 uv, float baseShapeRot, float maxInitVelMag=0, float gravity=0, Vector2? rotAccelMag=null) {
            this.baseShapeRot = baseShapeRot;
            this.uv = uv;
            baseLocation = location;
            var baseVel = M.CosSin(RNG.GetFloatOffFrame(0f, M.TAU)) * RNG.GetFloatOffFrame(0f, maxInitVelMag);
            offset = b => b.t * baseVel + new Vector2(0, -0.5f * b.t * b.t * gravity);
            
            bpi = new ParametricInfo(Vector2.zero, 0, 0, 0, FiringCtx.Empty);
            var rotationAccels = !rotAccelMag.Try(out var rA) ? Vector3.zero :
                M.Spherical(RNG.GetFloatOffFrame(0f, M.TAU), RNG.GetFloatOffFrame(0f, M.PI)) *
                RNG.GetFloatOffFrame(rA.x, rA.y);
            var rotationVels = RNG.GetFloatOffFrame(1f, 2f) * rotationAccels;
            rotations = t => t * rotationVels + 0.5f * t * t * rotationAccels;
        }
        
        public Fragment(Vector2 location, Vector2 uv, float baseShapeRot, TP offset, int index, Func<float, Vector3>? rotations, BPY? scale) {
            this.baseShapeRot = baseShapeRot;
            this.uv = uv;
            this.baseLocation = location;
            this.offset = offset;
            bpi = new ParametricInfo(Vector2.zero, index, RNG.GetUIntOffFrame(), 0, FiringCtx.Empty);
            this.rotations = rotations;
            this.scale = scale;
        }

        public void DoUpdate(float dT) {
            bpi.t += dT;
        }
    }

    public class FragmentRenderInstance {
        public readonly FragmentConfig config;
        public readonly Fragment[] fragments;
        public readonly MaterialPropertyBlock pb;
        private readonly Action? cb;
        private readonly Vector2 xBounds = new Vector2(-9, 9);
        private readonly Vector2 yBounds = new Vector2(-7, 7);
        public readonly int layer;
        public readonly int layerMask;
        private readonly float? aliveFor;
        private float time;

        public FragmentRenderInstance(FragmentConfig config, IEnumerable<Fragment> fragments, string? layerName, 
            Texture tex, Action? cb, Vector2? texSize=null, float? aliveFor=null) {
            this.config = config;
            this.fragments = fragments.ToArray();
            if (this.fragments.Length == 0)
                throw new Exception("Cannot setup FragmentRenderInstance with 0 fragments");
            layerName ??= "LowDirectRender";
            layer = LayerMask.NameToLayer(layerName);
            layerMask = LayerMask.GetMask(layerName);
            pb = new MaterialPropertyBlock();
            pb.SetTexture(PropConsts.mainTex, tex);
            pb.SetFloat(PropConsts.FragmentDiameter, config.FragmentDiameter);
            pb.SetFloat(PropConsts.FragmentSides, config.fragmentSides);
            var size = texSize ?? new Vector2(MainCamera.ScreenWidth, MainCamera.ScreenHeight);
            pb.SetFloat(PropConsts.texWidth, size.x);
            pb.SetFloat(PropConsts.texHeight, size.y);
            this.cb = cb;
            this.aliveFor = aliveFor;
        }

        /// <summary>
        /// Returns true if all fragments have been culled and this object can be deleted.
        /// Before the return, temporary RTs will be cleared and the callback will be called.
        /// If you keep executing this function after it returns true, errors may occur.
        /// </summary>
        /// <returns></returns>
        public bool DoUpdate() {
            time += ETime.FRAME_TIME;
            bool cullAllFragments = true;
            for (int fi = 0; fi < fragments.Length; ++fi) {
                var f = fragments[fi];
                f.DoUpdate(ETime.FRAME_TIME);
                if ((xBounds.x < f.bpi.loc.x && f.bpi.loc.x < xBounds.y) ||
                    (yBounds.x < f.bpi.loc.y && f.bpi.loc.y < yBounds.y))
                    cullAllFragments = false;
            }
            if (cullAllFragments || time > (aliveFor ?? float.PositiveInfinity)) {
                Destroy();
                return true;
            }
            return false;
        }

        public void Destroy() {
            for (int ii = 0; ii < fragments.Length; ++ii)
                fragments[ii].bpi.Dispose();
            cb?.Invoke();
        }
    }

    public static void Render(Camera c, FragmentRenderInstance? inst) {
        if (inst == null || inst.fragments.Length == 0) return;
        if ((c.cullingMask & inst.layerMask) == 0) return;
        var instanceCount = inst.fragments.Length;
        for (int done = 0; done < instanceCount; done += batchSize) {
            int run = Math.Min(instanceCount - done, batchSize);
            for (int batchInd = 0; batchInd < run; ++batchInd) {
                var obj = inst.fragments[done + batchInd];
                uvArr[batchInd] = new Vector4(obj.uv.x, obj.uv.y, obj.baseShapeRot, 0);
                var s = obj.Scale;
                matArr[batchInd] = Matrix4x4.TRS(obj.Location, Quaternion.Euler(obj.Rotation), new Vector3(s, s, s));
            }
            inst.pb.SetVectorArray(uvPropertyId, uvArr);
            CallInstancedDraw(c, inst.config.Mesh, inst.config.fragmentMaterial, inst.pb, run, inst.layer);
        }
    }

    private static void CallInstancedDraw(Camera c, Mesh m, Material mat, MaterialPropertyBlock pb, int ct, int layer) {
        UnityEngine.Graphics.DrawMeshInstanced(m, 0, mat,
            matArr,
            count: ct,
            properties: pb,
            castShadows: ShadowCastingMode.Off,
            receiveShadows: false,
            layer: layer,
            camera: c);
    }
    
    
    private static readonly Bounds drawBounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
    private static readonly int uvPropertyId = Shader.PropertyToID("uvRBuffer");
    private static readonly Vector4[] uvArr = new Vector4[batchSize];
    private static readonly Matrix4x4[] matArr = new Matrix4x4[batchSize];
    private const int batchSize = 511;
    
}
}