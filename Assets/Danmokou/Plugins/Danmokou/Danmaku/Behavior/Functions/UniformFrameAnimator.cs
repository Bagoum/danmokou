using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using UnityEngine;

namespace Danmokou.Behavior.Functions {
public class UniformFrameAnimator : RegularUpdater {

    private SpriteRenderer sr = null!;
    private float t;
    private int currSprite;

    public float timePerSprite;
    public Sprite[] sprites = null!;
    public bool destroyOnFinish;

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        sr.sprite = sprites[currSprite = 0];
    }

    public override void RegularUpdate() {
        t += ETime.FRAME_TIME;
        if (t >= timePerSprite) {
            while (t >= timePerSprite) {
                t -= timePerSprite;
                currSprite = (currSprite + 1) % sprites.Length;
                if (currSprite == 0 && destroyOnFinish) DisableDestroy();
            }
            sr.sprite = sprites[currSprite];
        }
    }
}
}