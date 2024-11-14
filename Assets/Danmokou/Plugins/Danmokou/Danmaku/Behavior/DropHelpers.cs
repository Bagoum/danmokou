using System;
using Danmokou.Behavior.Display;
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
    public static readonly IGradient Azure = DropLabel.MakeGradient(
        new Color32(100, 150, 255, 255), new Color32(80, 110, 255, 255));
    public static readonly IGradient Cyan = DropLabel.MakeGradient(
        new Color32(20, 220, 255, 255), new Color32(10, 170, 255, 255));
    public static readonly IGradient Green = DropLabel.MakeGradient(
        new Color32(0, 235, 162, 255), new Color32(0, 172, 70, 255));
    public static readonly IGradient Red = DropLabel.MakeGradient(
        new Color32(255, 10, 138, 255), new Color32(240, 0, 52, 255));
    private const float LABEL_RAD = 0.67f;
    public static void DropDropLabel(Vector2 baseLoc, IGradient color, string text, float ttl, float speed=0.4f, float size=1f) {
        var req = new LabelRequestContext(baseLoc, LABEL_RAD, speed, RNG.GetFloatOffFrame(40, 140), ttl, color, text, size);
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