using System.Linq;
using UnityEngine;

namespace Danmokou.Graphics {
public static class MeshGenerator {
    public readonly struct RenderInfo {
        public readonly Material material;
        public readonly Mesh mesh;
        public RenderInfo(Material mat, Mesh m, bool instantiate) {
            this.material = instantiate ? Object.Instantiate(mat) : mat;
            this.mesh = m;
        }

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
        public static RenderInfo FromSprite(Material baseMaterial, Sprite s, int priority = 0) {
            var renderMaterial = Object.Instantiate(baseMaterial);
            renderMaterial.enableInstancing = true;
            renderMaterial.SetTexture(PropConsts.mainTex, s.texture);
            renderMaterial.renderQueue += priority;
            return new RenderInfo(renderMaterial, FromSprite(s), false);
        }
    }

}
}
