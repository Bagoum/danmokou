using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Danmaku {
public class GemItem : Item {
    protected override short RenderOffsetIndex => 0;
    public int numGems = 1;
    
    private float t;
    private int currSprite;

    public float timePerSprite;
    public Sprite[] sprites;

    protected override void Awake() {
        base.Awake();
        sr.sprite = sprites[currSprite = 0];
    }

    protected override void CollectMe() {
        GameManagement.campaign.AddGems(numGems);
        base.CollectMe();
    }

    public override void ResetV() {
        base.ResetV();
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