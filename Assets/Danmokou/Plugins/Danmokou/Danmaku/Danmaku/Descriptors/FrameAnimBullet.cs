using System;
using System.Linq;
using Danmokou.Core;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;


namespace Danmokou.Danmaku.Descriptors {
public class FrameAnimBullet : ColorizableBullet {
//Inspector-exposed structs cannot be readonly
    [Serializable]
    public struct BulletAnimSprite {
        public Sprite s;
        public float time;
        public bool collisionActive;
        public float yscale;
    }

    public readonly struct Recolor {
        public readonly BulletAnimSprite[]? sprites;
        public readonly GameObject prefab;
        public readonly Material material;
        public readonly string style;

        public Recolor(BulletAnimSprite[]? sprites, GameObject prefab, Material mat, string style) {
            this.sprites = sprites;
            this.prefab = prefab;
            this.material = mat;
            this.style = style;
        }
    }
    public BulletAnimSprite[] frames = null!;
    public BulletAnimSprite[] Frames => frames;
    private BulletAnimSprite[] realizedFrames = null!;
    private int currFrame = 0;
    private float frameTime = 0f;
    public int coldFrame;
    public int hotFrame;
    public bool repeat = true;

    protected override void ResetValues() {
        base.ResetValues();
        frameTime = 0f;
        realizedFrames = frames.ToArray();
        SetFrame(0);
    }

    protected override void RegularUpdateRender() {
        base.RegularUpdateRender();
        frameTime += ETime.FRAME_TIME;
        while (frameTime >= realizedFrames[currFrame].time) {
            frameTime -= realizedFrames[currFrame].time;
            if (SetFrame(currFrame + 1)) {
                return;
            } //Avoid calling InvokeCull here and in base.Upd
        }
    }

    private static float Share(float claim, ref float remaining) {
        float claimable = Mathf.Min(claim, remaining);
        remaining -= claimable;
        return claimable;
    }

    private string? hotSfx;
    protected void SetColdHot(float cold, float hot, string? sfxOnHot=null, bool rpt=false) {
        repeat = rpt;
        hotSfx = sfxOnHot;
        //For cold=0, use the transition cold frames, but set them to hot.
        //For hot=0, switch the hot and cold frames, scale down all the sizes, set all frames off, and animate normally.
        var (c, h) = (coldFrame, hotFrame);
        if (hot < float.Epsilon) {
            var sizeMul = realizedFrames[coldFrame].yscale;
            for (int ii = 0; ii < realizedFrames.Length; ++ii) {
                realizedFrames[ii].yscale *= sizeMul;
                realizedFrames[ii].collisionActive = false;
            }
            (c, h) = (hotFrame, coldFrame);
        }
        var m = Math.Max(c, h);
        for (int ii = 0; ii < realizedFrames.Length; ++ii) {
            if (ii == c || ii == h) {
            } else if (realizedFrames[ii].collisionActive) {
                realizedFrames[ii].time = Share(realizedFrames[ii].time, ref hot);
            } else if (ii < m) {
                cold -= realizedFrames[ii].time;
                if (cold < 0) realizedFrames[ii].collisionActive = true;
            }
        }
        realizedFrames[c].time = Mathf.Max(0f, cold);
        realizedFrames[h].time = hot;
    }

    private bool SetFrame(int f) {
        if (f < realizedFrames.Length) {
            currFrame = f;
            if (realizedFrames[f].collisionActive && !collisionActive && realizedFrames[f].time > 0f) {
                ServiceLocator.SFXService.Request(hotSfx);
            }
            collisionActive = realizedFrames[f].collisionActive;
            SetSprite(realizedFrames[f].s, realizedFrames[f].yscale);
        } else if (repeat) {
            SetFrame(0);
        } else {
            InvokeCull();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Call this before SetHotCold, as this resets the frame timings.
    /// </summary>
    /// <param name="r"></param>
    protected override void Colorize(Recolor r) {
        SetMaterial(r.material);
        if (r.sprites != null) {
            for (int ii = 0; ii < r.sprites.Length; ++ii) {
                realizedFrames[ii] = r.sprites[ii];
            }
        }
        SetFrame(currFrame);
    }

    public override void ColorizeOverwrite(Recolor r) {
        SetMaterial(r.material);
        if (r.sprites != null) {
            for (int ii = 0; ii < r.sprites.Length; ++ii) {
                realizedFrames[ii].s = r.sprites[ii].s;
            }
        }
        SetFrame(currFrame);
    }

    protected override void SetSprite(Sprite s) => SetSprite(s, 1f);
    protected virtual void SetSprite(Sprite s, float yscale) => base.SetSprite(s);

}
}