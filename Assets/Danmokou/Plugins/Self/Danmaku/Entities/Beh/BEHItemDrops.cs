using UnityEngine;
using DMath;

namespace Danmaku {
public partial class BehaviorEntity {
    private static void DropEvenly(Enums.ItemType t, Vector2 baseLoc, int count, bool autocollect, float r, float a0) {
        for (int ii = 0; ii < count; ++ii) {
            ItemPooler.RequestItem(new ItemRequestContext(baseLoc, r * M.CosSinDeg(a0 + ii * 360f / count)), t)
                ?.Autocollect(autocollect || t.Autocollect());
        }
    }

    private void DropEvenly(Enums.ItemType t, int count, bool autocollect, float r, float a0 = 90f) => 
        DropEvenly(t, bpi.loc, count, autocollect, r, a0);

#if UNITY_EDITOR
    [ContextMenu("Drop some power")]
    public void DropSomePower() => DropEvenly(Enums.ItemType.POWER, 19, false, 0.4f);
    [ContextMenu("Drop some life")]
    public void DropSomeLife() => DropEvenly(Enums.ItemType.LIFE, 100, false, 0.4f);
    [ContextMenu("Drop some value")]
    public void DropSomeValue() => DropEvenly(Enums.ItemType.VALUE, 400, false, 0.4f);
    [ContextMenu("Drop some PPP")]
    public void DropSomePPP() => DropEvenly(Enums.ItemType.PPP, 200, false, 0.4f);
#endif
    [ContextMenu("Drop a full power")]
    public void DropFullPower() => DropOne(Enums.ItemType.FULLPOWER);
    [ContextMenu("Drop 1UP")]
    public void Drop1UP() => DropOne(Enums.ItemType.ONEUP);
    [ContextMenu("Drop powerup D")]
    public void DropPowerupD() => DropOne(Enums.ItemType.POWERUP_D);
    [ContextMenu("Drop powerup M")]
    public void DropPowerupM() => DropOne(Enums.ItemType.POWERUP_M);
    [ContextMenu("Drop powerup K")]
    public void DropPowerupK() => DropOne(Enums.ItemType.POWERUP_K);
    [ContextMenu("Drop shifting powerup")]
    public void DropPowerupShift() => DropOne(Enums.ItemType.POWERUP_SHIFT);

    public void DropOne(Enums.ItemType type) =>
        ItemPooler.RequestItem(new ItemRequestContext(rBPI.loc, Vector2.zero), type);
    
    
    private void _DropItems(ItemDrops drops, float rValue=0.4f, float rPPP=0f, float rLife=0.2f, float rPower=0.6f, float rGems=0.8f) {
        DropEvenly(Enums.ItemType.VALUE, drops.value, drops.autocollect, rValue);
        DropEvenly(Enums.ItemType.POWER, drops.power, drops.autocollect, rPower);
        DropEvenly(Enums.ItemType.PPP, drops.pointPP, drops.autocollect, rPPP);
        DropEvenly(Enums.ItemType.LIFE, drops.life, drops.autocollect, rLife);
        DropEvenly(Enums.ItemType.GEM, drops.gems, drops.autocollect, rGems);
    }

    public void DropItems(ItemDrops? drops, float rValue, float rPPP, float rLife, float rPower, float rGems) {
        if (drops.HasValue) _DropItems(drops.Value, rValue, rPPP, rLife, rPower, rGems);
    }
    public void DropItems(ItemDrops? drops) {
        if (drops.HasValue) _DropItems(drops.Value);
    }

    private ItemDrops? deathDrops = null;
    private void DropItemsOnDeath() => DropItems((enemy == null) ? deathDrops : deathDrops ?? enemy.AutoDeathItems);

}
}