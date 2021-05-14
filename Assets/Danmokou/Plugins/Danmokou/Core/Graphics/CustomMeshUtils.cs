using UnityEngine;

namespace Danmokou.Graphics {
public static class CustomMeshUtils {

    private static Vector3[] Norms(int n) {
        Vector3[] norms = new Vector3[n];
        for (int ii = 0; ii < n; ++ii) {
            norms[ii] = Vector3.forward;
        }
        return norms;
    }

    public static Vector3[] WHNorms(int h, int w) {
        int n = (h + 1) * (w + 1);
        return Norms(n);
    }

    public static int[] WHTris(int h, int w) {
        int vw = w + 1;
        int[] tris = new int[2 * h * w * 3];
        for (int ih = 0; ih < h; ++ih) {
            for (int iw = 0; iw < w; ++iw) {
                int it = 2 * (w * ih + iw) * 3;
                int iv = ih * vw + iw;
                tris[it + 0] = iv;
                tris[it + 1] = iv + vw + 1;
                tris[it + 2] = iv + 1;

                tris[it + 3] = iv + vw + 1;
                tris[it + 4] = iv;
                tris[it + 5] = iv + vw;
            }
        }
        return tris;
    }

    public static Vector2[] WHUV(int h, int w) {
        int vh = h + 1;
        int vw = w + 1;
        Vector2[] uvs = new Vector2[vh * vw];
        for (int ih = 0; ih < vh; ++ih) {
            for (int iw = 0; iw < vw; ++iw) {
                uvs[ih * vw + iw] = new Vector2((float) iw / w, (float) ih / h);
            }
        }
        return uvs;
    }

}
}