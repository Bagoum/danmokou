using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;


namespace Danmaku {
public class FrameAnimBullet : Bullet {
//Inspector-exposed structs cannot be readonly
    [System.Serializable]
    public struct BulletAnimSprite {
        public Sprite s;
        public float time;
        public bool collisionActive;
        public float yscale;
    }

    public readonly struct Recolor {
        [CanBeNull] public readonly BulletAnimSprite[] sprites;
        public readonly GameObject prefab;
        public readonly Material material;
        public readonly string style;

        public Recolor([CanBeNull] BulletAnimSprite[] sprites, GameObject prefab, Material mat, string style) {
            this.sprites = sprites;
            this.prefab = prefab;
            this.material = mat;
            this.style = style;
        }
    }

    public float fadeInTime;
    public float cycleSpeed;
    public RenderMode renderMode;
    public DefaultColorizing colorizing;
    public SimpleBulletEmptyScript.DisplacementInfo displacement;
    [Tooltip("Special gradients")] public BulletManager.GradientVariant[] gradients;
    public BulletAnimSprite[] defaultFrames;
    private BulletAnimSprite[] frames;
    private int currFrame = 0;
    private float frameTime = 0f;
    public int coldFrame;
    public int hotFrame;
    public bool repeat = true;

    public override void ResetV() {
        base.ResetV();
        frameTime = 0f;
        frames = defaultFrames.ToArray();
        SetFrame(0);
    }

    protected override void RegularUpdateRender() {
        base.RegularUpdateRender();
        frameTime += ETime.FRAME_TIME;
        while (frameTime >= frames[currFrame].time) {
            frameTime -= frames[currFrame].time;
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

    [CanBeNull] private string hotSfx;
    public bool animateCold;
    protected void SetColdHot(float cold, float hot, [CanBeNull] string sfxOnHot=null, bool rpt=false) {
        repeat = rpt;
        hotSfx = sfxOnHot;
        //For cold=0, use the transition cold frames.
        //For hot=0, use only the single main cold frame, unless ANIMATE_COLD is set.
        if (hot < float.Epsilon && !animateCold) {
            for (int ii = 0; ii < frames.Length; ++ii) frames[ii].time = 0f;
            frames[coldFrame].time = cold;
        } else {
            for (int ii = 0; ii < frames.Length; ++ii) {
                if (ii == coldFrame || ii == hotFrame) {
                } else if (frames[ii].collisionActive) {
                    frames[ii].time = Share(frames[ii].time, ref hot);
                } else if (countPostHotColdFrames || ii < hotFrame) {
                    cold -= frames[ii].time;
                }
            }
            frames[coldFrame].time = Mathf.Max(0f, cold);
            frames[hotFrame].time = hot;
        }
    }

    public bool countPostHotColdFrames;

    private bool SetFrame(int f) {
        if (f < frames.Length) {
            currFrame = f;
            if (frames[f].collisionActive && !collisionActive && frames[f].time > 0f) {
                SFXService.Request(hotSfx);
            }
            collisionActive = frames[f].collisionActive;
            SetSprite(frames[f].s, frames[f].yscale);
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
    public void Colorize(Recolor r) {
        style = r.style;
        if (r.sprites == null) return;
        material = r.material;
        for (int ii = 0; ii < r.sprites.Length; ++ii) {
            frames[ii] = r.sprites[ii];
        }
        SetFrame(currFrame);
    }

    protected virtual void SetSprite(Sprite s, float yscale) {
        //sprite renderer or something
    }
}
}