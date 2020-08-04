namespace Danmaku {
public class CurvedTileRenderLightningPather : CurvedTileRenderPather {
    public float XBlocksPerUnit = 10f;

    /* Not necessary with location-dependent randomness
    protected override unsafe void UpdateVerts(bool renderRequired) {
        base.UpdateVerts(renderRequired);
        if (renderRequired) {
            var diff = centers[texRptWidth] - centers[read_from];
            pb.SetFloat(PropConsts.xBlocks, diff.magnitude * XBlocksPerUnit);
        }
    }*/
}
}