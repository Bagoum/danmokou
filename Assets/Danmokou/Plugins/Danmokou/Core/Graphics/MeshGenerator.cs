using System.Linq;
using UnityEngine;

namespace Danmokou.Graphics {
public static class MeshGenerator {
    public readonly struct RenderInfo {
        public Material Material { get; }
        public Mesh Mesh { get; }
        public Sprite? Sprite { get; }
        public RenderInfo(Material mat, Mesh m, Sprite? sourceSprite = null) {
            this.Material = mat;
            this.Mesh = m;
            this.Sprite = sourceSprite;
        }

        public RenderInfo Copy() => new(Object.Instantiate(Material), Mesh, Sprite);

        public static Mesh FromSprite(Sprite s, float scale = 1f) {
            Vector2[] orig_verts = s.vertices;
            Vector3[] verts = new Vector3[orig_verts.Length];
            for (int ii = 0; ii < verts.Length; ++ii) verts[ii] = orig_verts[ii] * scale;
            ushort[] orig_tris = s.triangles;
            int[] tris = new int[orig_tris.Length];
            for (int ii = 0; ii < tris.Length; ++ii) tris[ii] = orig_tris[ii];
            return new Mesh {
                vertices = verts,
                triangles = tris,
                uv = s.uv
            };
        }
        public static RenderInfo FromSprite(Material baseMaterial, Sprite s) {
            var renderMaterial = Object.Instantiate(baseMaterial);
            renderMaterial.enableInstancing = true;
            renderMaterial.SetTexture(PropConsts.mainTex, s.texture);
            return new RenderInfo(renderMaterial, FromSprite(s), s);
        }

    }

}
}
