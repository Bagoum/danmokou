using System;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Pooling;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou.Behavior {
[Serializable]
public struct ItemDrops {
    public double value;
    public int pointPP;
    public int life;
    public int power;
    public int gems;
    public bool autocollect;
    public ItemDrops(double v, int pp, int l, int pow, int gem, bool autoc=false) {
        value = v;
        pointPP = pp;
        life = l;
        power = pow;
        gems = gem;
        autocollect = autoc;
    }

    public ItemDrops Mul(float by) => new((value * by), (int)(pointPP * by), (int)(life * by), 
        (int)(power * by), (int)(gems * by), autocollect);
}

public static class DropHelpers {
    private const float LABEL_RAD = 0.67f;
    public static void DropDropLabel(Vector2 baseLoc, IGradient color, string text, float speed=0.4f, float ttl=0.7f, float multiplier=1f) {
        var req = new LabelRequestContext(baseLoc, LABEL_RAD, speed, RNG.GetFloatOffFrame(40, 140), ttl, color, text, multiplier);
        ItemPooler.RequestLabel(req);
    }
    public static void DropOne(ItemType type, Vector2 baseLoc) =>
        ItemPooler.RequestItem(new ItemRequestContext(baseLoc, Vector2.zero), type);
    
    public static void DropEvenly(ItemType t, Vector2 baseLoc, int count, bool autocollect, float r, float a0 = 90f) {
        for (int ii = 0; ii < count; ++ii) {
            ItemPooler.RequestItem(new ItemRequestContext(baseLoc, r * M.CosSinDeg(a0 + ii * 360f / count)), t)
                ?.Autocollect(autocollect || t.Autocollect());
        }
    }
    
    public static void DropEvenly(ItemType t, ItemType small, double smallRatio, Vector2 baseLoc, 
        double count, bool autocollect, float r, float a0 = 90f) {
        int fullCt = (int)Math.Floor(count);
        int smallCt = (int) Math.Floor((count - fullCt) / smallRatio);
        var step = 360f / (smallCt + fullCt);
        if (fullCt > 0) a0 -= (fullCt - 1f) / 2f * step;
        for (int ii = 0; ii < smallCt + fullCt; ++ii) {
            ItemPooler.RequestItem(new ItemRequestContext(baseLoc, r * M.CosSinDeg(a0 + ii * step)), ii < fullCt ? t : small)
                ?.Autocollect(autocollect || t.Autocollect());
        }
    }
    
    public static void DropItems(ItemDrops drops, Vector2 baseLoc, float rValue=0.4f, float rPPP=0f, float rLife=0.2f, float rPower=0.6f, float rGems=0.8f) {
        DropEvenly(ItemType.VALUE, ItemType.SMALL_VALUE, GameManagement.Instance.ScoreF.SmallValueRatio, baseLoc, drops.value, drops.autocollect, rValue);
        DropEvenly(ItemType.POWER, baseLoc, drops.power, drops.autocollect, rPower);
        DropEvenly(ItemType.PPP, baseLoc, drops.pointPP, drops.autocollect, rPPP);
        DropEvenly(ItemType.LIFE, baseLoc, drops.life, drops.autocollect, rLife);
        DropEvenly(ItemType.GEM, baseLoc, drops.gems, drops.autocollect, rGems);
    }
}
}