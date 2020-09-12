using UnityEngine;
using DMath;

namespace Danmaku {
public partial class BehaviorEntity {
    private static void DropEvenly(ItemType t, Vector2 baseLoc, int count, bool autocollect, float r, float a0 = -90f) {
        for (int ii = 0; ii < count; ++ii) {
            ItemPooler.RequestItem(baseLoc + r * M.CosSinDeg(a0 + ii * 360f / count), t)?.Autocollect(autocollect);
        }
    }

    private void DropEvenly(ItemType t, int count, bool autocollect, float r, float a0 = -90f) => 
        DropEvenly(t, bpi.loc, count, autocollect, r, a0);
    private static void _DropLifeItems(Vector2 baseLoc, int count, float lowR=0.1f, float highR=0.3f) {
        for (int ii = 0; ii < count; ++ii) ItemPooler.RequestLife(baseLoc + RNG.GetPointInCircle(lowR, highR));
    }
    private static void _DropValueItems(Vector2 baseLoc, int count, float lowR=0.3f, float highR=0.5f) {
        for (int ii = 0; ii < count; ++ii) ItemPooler.RequestValue(baseLoc + RNG.GetPointInCircle(lowR, highR));
    }
    private static void _DropPointPPItems(Vector2 baseLoc, int count, float lowR=0.5f, float highR=0.9f) {
        for (int ii = 0; ii < count; ++ii) ItemPooler.RequestPointPP(baseLoc + RNG.GetPointInCircle(lowR, highR));
    }
    private static void _DropPowerItems(Vector2 baseLoc, int count, float lowR=0.7f, float highR=1.1f) {
        for (int ii = 0; ii < count; ++ii) ItemPooler.RequestPower(baseLoc + RNG.GetPointInCircle(lowR, highR));
    }

    [ContextMenu("Drop some power")]
    public void DropSomePower() => _DropPowerItems(bpi.loc, 19);
    [ContextMenu("Drop some life")]
    public void DropSomeLife() => _DropLifeItems(bpi.loc, 100, 0.4f, 0.8f);
    [ContextMenu("Drop some value")]
    public void DropSomeValue() => _DropValueItems(bpi.loc, 400, 0.2f, 0.5f);
    [ContextMenu("Drop some PPP")]
    public void DropSomePPP() => _DropPointPPItems(bpi.loc, 200, 0.7f, 1f);

    private void _DropItems(ItemDrops drops, float rValue=0.4f, float rPPP=0f, float rLife=0.2f, float rPower=0.6f) {
        DropEvenly(ItemType.VALUE, drops.value, drops.autocollect, rValue);
        DropEvenly(ItemType.POWER, drops.power, drops.autocollect, rPower);
        DropEvenly(ItemType.PPP, drops.pointPP, drops.autocollect, rPPP);
        DropEvenly(ItemType.LIFE, drops.life, drops.autocollect, rLife);
    }

    public void DropItems(ItemDrops? drops, float rValue, float rPPP, float rLife) {
        if (drops.HasValue) _DropItems(drops.Value, rValue, rPPP, rLife);
    }
    public void DropItems(ItemDrops? drops) {
        if (drops.HasValue) _DropItems(drops.Value);
    }

    private ItemDrops? deathDrops = null;
    private void DropItemsOnDeath() => DropItems((enemy == null) ? deathDrops : deathDrops ?? enemy.AutoDeathItems);

}
}