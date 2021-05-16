using Danmokou.Core;
using Danmokou.Player;
using UnityEngine;

namespace Danmokou.Behavior.Items {
public class GemItem : Item {
    protected override ItemType Type => ItemType.GEM;
    protected override float MinTimeBeforeHome => 1.1f;
    protected override short RenderOffsetIndex => 0;
    public int numGems = 1;
    
    private float t;
    private int currSprite;

    public float timePerSprite;
    public Sprite[] sprites = null!;

    protected override void CollectMe(PlayerController collector) {
        collector.AddGems(numGems);
        base.CollectMe(collector);
    }

    protected override void ResetValues() {
        base.ResetValues();
        t = 0;
        sr.sprite = sprites[currSprite = 0];
    }
    
    public override void RegularUpdate() {
        base.RegularUpdate();
        t += ETime.FRAME_TIME;
        if (t >= timePerSprite) {
            while (t >= timePerSprite) {
                t -= timePerSprite;
                currSprite = (currSprite + 1) % sprites.Length;
            }
            sr.sprite = sprites[currSprite];
        }
    }
}
}