using System;
using System.Reflection;
using BagoumLib;
using Danmokou.Core;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using InternalSetVertexData = System.Action<UnityEngine.Mesh, 
    int, System.IntPtr, int, int, int, int, UnityEngine.Rendering.MeshUpdateFlags>;

namespace Danmokou.Graphics {
[Serializable]
public class TiledRenderCfg {
    public string sortingLayer = "";
    public string playerSortingLayer = "";
    public float dontUpdateTimeAfter;
}

public abstract class TiledRender {
    protected readonly MaterialPropertyBlock pb;
    protected readonly Renderer render;
    private readonly MeshFilter mf;
    private Mesh mesh = null!;
    protected readonly Transform tr;
    protected ITransformHandler locater = null!;
    protected bool parented;

    protected int texRptHeight;
    protected int texRptWidth;
    private int numVerts;
    protected virtual bool HandleAsMesh => true;

    protected Vector2 spriteBounds;

    protected float lifetime = 0f;
    public void SetLifetime(float t) => lifetime = t;
    /// <summary>
    /// Updating PropertyBlock is expensive-- better to skip it once time-based effects are in place.
    /// </summary>
    protected float DontUpdateTimeAfter;
    private bool active = false;
    private bool isStatic;

    private static short renderCounter = short.MinValue;

    private const MeshUpdateFlags noValidation = MeshUpdateFlags.DontRecalculateBounds |
                                                 MeshUpdateFlags.DontValidateIndices |
                                                 MeshUpdateFlags.DontNotifyMeshUsers |
                                                 MeshUpdateFlags.DontResetBoneBounds;

    private static readonly VertexAttributeDescriptor[] layout = {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
    };

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    protected struct VertexData {
        public Vector3 loc;
        public Vector2 uv;
    }

    private static readonly int VertexDataSize = UnsafeUtility.SizeOf<VertexData>();

    protected NativeArray<VertexData> verts;
    protected unsafe VertexData* vertsPtr = (VertexData*) 0x0;
    private IntPtr roVertsPtr = (IntPtr) 0x0;

    protected TiledRender(GameObject obj) {
        pb = new MaterialPropertyBlock();
        render = obj.GetComponent<Renderer>();
        mf = obj.GetComponent<MeshFilter>();
        tr = obj.transform;
    }

    //TileRenders are always Initialize-initialized.
    protected void Initialize(ITransformHandler locationer, TiledRenderCfg cfg, Material material, bool is_static,
        bool isPlayer) {
        locater = locationer;
        parented = locater.HasParent();
        isStatic = is_static;
        material.enableInstancing = false; //Instancing doesn't work with this, and it has overhead, so disable it.
        render.sharedMaterial = material;
        render.sortingOrder = renderCounter++;
        render.sortingLayerID = SortingLayer.NameToID(isPlayer ? cfg.playerSortingLayer : cfg.sortingLayer);
        DontUpdateTimeAfter = cfg.dontUpdateTimeAfter;
        //mr.GetPropertyBlock(pb);
    }

#if UNITY_EDITOR
    [ContextMenu("Get Sorting ID")]
    public void sortId() {
        Logs.Log(render.sortingOrder.ToString(), level: LogLevel.INFO);
    }
#endif


    //Queried every frame (in subclasses); therefore we store an array and update it.
    protected abstract void UpdateVerts(bool renderRequired);

    protected void PrepareNewMesh() {
        if (!HandleAsMesh) return;
        numVerts = (texRptHeight + 1) * (texRptWidth + 1);
        int[] tris = CustomMeshUtils.WHTris(texRptHeight, texRptWidth);
        mf.mesh = mesh = new Mesh();
        mesh.SetVertexBufferParams(numVerts, layout);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        DisposeOldVerts();
        mesh.MarkDynamic();
        verts = new NativeArray<VertexData>(numVerts, Allocator.Persistent);
        unsafe {
            vertsPtr = (VertexData*) verts.GetUnsafePtr();
            roVertsPtr = (IntPtr) verts.GetUnsafeReadOnlyPtr();
        }
        ReassignMeshVerts();
        mesh.triangles = tris;
        OnNewMesh();
    }

    protected virtual void OnNewMesh() { }

    //This is not very nice, but it's way faster than using the official interface.
    private static readonly InternalSetVertexData SetVertexBufferData = (InternalSetVertexData)
        Delegate.CreateDelegate(typeof(InternalSetVertexData),
            typeof(Mesh).GetMethod("InternalSetVertexBufferData",
                BindingFlags.NonPublic | BindingFlags.Instance)!);

    //Do not use this to change rh/rw, it will break
    //Use this only to change vertex size/position
    private void UpdateVertsAndMesh() {
        UpdateVerts(true);
        ReassignMeshVerts();
    }

    private void ReassignMeshVerts() {
        if (HandleAsMesh) SetVertexBufferData(mesh, 0, roVertsPtr, 0, 0, numVerts, VertexDataSize, noValidation);
        //mesh.SetVertexBufferData(verts, 0, 0, numVerts, 0, noValidation);

        //Don't recalculate mesh bounds-- just set them to max from the start. Based on testing,
        //using large mesh bounds does not incur any costs (and it saves assignment overhead).
        //mesh.bounds = bds;
        //m.RecalculateBounds(); 
    }

    public void DebugMeshBounds() {
        Debug.Log(mesh.bounds);
    }

    public virtual void UpdateMovement(float dT) {
        lifetime += dT;
        if (!isStatic) {
            UpdateVerts(ETime.LastUpdateForScreen);
        }
    }

    public virtual void UpdateRender() {
        if (ETime.LastUpdateForScreen) {
            if (!isStatic && HandleAsMesh) {
                //Inlined from ReassignMeshVerts
                SetVertexBufferData(mesh, 0, roVertsPtr, 0, 0, numVerts, VertexDataSize, noValidation);
            }
            if (lifetime < DontUpdateTimeAfter) {
                pb.SetFloat(PropConsts.time, lifetime);
                render.SetPropertyBlock(pb);
            }
        }
    }

    protected float PersistentYScale = 1f;

    /// <summary>
    /// This will update the mesh iff the sprite has a different size.
    /// </summary>
    public virtual void SetSprite(Sprite s, float yscale) {
        bool diffSize = spriteBounds != (Vector2) s.bounds.size;
        spriteBounds.x = s.bounds.size.x;
        spriteBounds.y = s.bounds.size.y * yscale * PersistentYScale;
        if (diffSize && active && isStatic) {
            //if not static, then will update in next update call
            UpdateVertsAndMesh();
        }
        pb.SetTexture(PropConsts.mainTex, s.texture);
        //but make sure that the PB is updated regardless, since
        //that might not occur next frame
        render.SetPropertyBlock(pb);
    }

    public virtual void Deactivate() {
        if (HandleAsMesh) render.enabled = false;
        active = false;
        isStatic = true;
    }

    public virtual void Activate() {
        lifetime = 0f;
        if (HandleAsMesh) render.enabled = true;
        UpdateVertsAndMesh();
        pb.SetFloat(PropConsts.time, 0f);
        render.SetPropertyBlock(pb);
        active = true;
    }

    private unsafe void DisposeOldVerts() {
        if (vertsPtr != (VertexData*) 0x0) verts.Dispose();
    }

    public void Destroy() => DisposeOldVerts();

#if UNITY_EDITOR
    [ContextMenu("Debug sizes")]
    public void DebugSizes() {
        unsafe {
            Debug.Log(sizeof(VertexData));
            Debug.Log($"unsafeutility {sizeof(VertexData)}");
        }
    }

#endif
}
}
