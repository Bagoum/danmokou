using System;
using Danmokou.Core;
using UnityEngine;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Pooling;

namespace Danmokou.Behavior {
public partial class BehaviorEntity {
    

    private const float LABEL_RAD = 0.67f;
    public void DropDropLabel(IGradient color, string text, float speed=0.4f, float ttl=0.7f, float multiplier=1f) {
        var req = new LabelRequestContext(Loc, LABEL_RAD, speed, RNG.GetFloatOffFrame(40, 140), ttl, color, text, multiplier);
        ItemPooler.RequestLabel(req);
    }
    
    private static void DropEvenly(ItemType t, Vector2 baseLoc, int count, bool autocollect, float r, float a0) {
        for (int ii = 0; ii < count; ++ii) {
            ItemPooler.RequestItem(new ItemRequestContext(baseLoc, r * M.CosSinDeg(a0 + ii * 360f / count)), t)
                ?.Autocollect(autocollect || t.Autocollect());
        }
    }
    private static void DropEvenly(ItemType t, ItemType small, double smallRatio, Vector2 baseLoc, 
        double count, bool autocollect, float r, float a0) {
        int fullCt = (int)Math.Floor(count);
        int smallCt = (int) Math.Floor((count - fullCt) / smallRatio);
        var step = 360f / (smallCt + fullCt);
        if (fullCt > 0) a0 -= (fullCt - 1f) / 2f * step;
        for (int ii = 0; ii < smallCt + fullCt; ++ii) {
            ItemPooler.RequestItem(new ItemRequestContext(baseLoc, r * M.CosSinDeg(a0 + ii * step)), ii < fullCt ? t : small)
                ?.Autocollect(autocollect || t.Autocollect());
        }
    }

    private void DropEvenly(ItemType t, int count, bool autocollect, float r, float a0 = 90f) => 
        DropEvenly(t, Loc, count, autocollect, r, a0);
    private void DropEvenly(ItemType t, ItemType s, double exchange, double count, bool autocollect, 
        float r, float a0 = 90f) => 
        DropEvenly(t, s, exchange, Loc, count, autocollect, r, a0);

#if UNITY_EDITOR
    [ContextMenu("Drop some power")]
    public void DropSomePower() => DropEvenly(ItemType.POWER, 19, false, 0.4f);
    [ContextMenu("Drop some life")]
    public void DropSomeLife() => DropEvenly(ItemType.LIFE, 100, false, 0.4f);
    [ContextMenu("Drop some value")]
    public void DropSomeValue() => DropEvenly(ItemType.VALUE, ItemType.SMALL_VALUE, 
        InstanceConsts.smallValueRatio, 1.4, false, 0.9f);
    [ContextMenu("Drop some PPP")]
    public void DropSomePPP() => DropEvenly(ItemType.PPP, 200, false, 0.4f);
#endif
    [ContextMenu("Drop a full power")]
    public void DropFullPower() => DropOne(ItemType.FULLPOWER);
    [ContextMenu("Drop 1UP")]
    public void Drop1UP() => DropOne(ItemType.ONEUP);
    [ContextMenu("Drop powerup D")]
    public void DropPowerupD() => DropOne(ItemType.POWERUP_D);
    [ContextMenu("Drop powerup M")]
    public void DropPowerupM() => DropOne(ItemType.POWERUP_M);
    [ContextMenu("Drop powerup K")]
    public void DropPowerupK() => DropOne(ItemType.POWERUP_K);
    [ContextMenu("Drop shifting powerup")]
    public void DropPowerupShift() => DropOne(ItemType.POWERUP_SHIFT);

    public void DropOne(ItemType type) =>
        ItemPooler.RequestItem(new ItemRequestContext(rBPI.loc, Vector2.zero), type);
    
    
    private void _DropItems(ItemDrops drops, float rValue=0.4f, float rPPP=0f, float rLife=0.2f, float rPower=0.6f, float rGems=0.8f) {
        DropEvenly(ItemType.VALUE, ItemType.SMALL_VALUE, InstanceConsts.smallValueRatio, drops.value, drops.autocollect, rValue);
        DropEvenly(ItemType.POWER, drops.power, drops.autocollect, rPower);
        DropEvenly(ItemType.PPP, drops.pointPP, drops.autocollect, rPPP);
        DropEvenly(ItemType.LIFE, drops.life, drops.autocollect, rLife);
        DropEvenly(ItemType.GEM, drops.gems, drops.autocollect, rGems);
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