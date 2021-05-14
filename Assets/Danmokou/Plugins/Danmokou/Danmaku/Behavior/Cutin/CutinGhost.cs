using Danmokou.Core;
using Danmokou.Graphics;
using Danmokou.Pooling;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class CutinGhost : Pooled<CutinGhost> {
    /*
     * 
            _Scale("Scale", Float) = 1.0
            _AIBS("Blur Step", Range(0.0001, 0.1)) = 0.1
            _BlurRad("Blur Radius", Float)  = 1.0
     * */
    private float time;
    private Cutin.GhostConfig cfg;
    private Vector3 velocity;
    private MaterialPropertyBlock pb = default!;
    private SpriteRenderer sr = default!;

    protected override void Awake() {
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        base.Awake();
    }

    public void Initialize(Vector2 location, Vector2 vel, Cutin.GhostConfig config) {
        time = 0;
        tr.position = location;
        this.velocity = vel;
        cfg = config;
        sr.sprite = cfg.sprite;
        pb.SetTexture(PropConsts.mainTex, cfg.sprite.texture);
        RegularUpdate();
    }

    // Update is called once per frame
    public override void RegularUpdate() {
        if (ETime.LastUpdateForScreen) {
            time += ETime.dT;
            tr.localPosition += velocity * ETime.dT;
            float ratio = time / cfg.ttl;
            Color c = Color.Lerp(cfg.startColor, cfg.endColor, ratio);
            c.a *= 1f - ratio;
            sr.color = c;
            float s = Mathf.Lerp(cfg.scale.x, cfg.scale.y, ratio);
            tr.localScale = new Vector3(s, s, 1.0f);
            float b = Mathf.Lerp(cfg.blurRad.x, cfg.blurRad.y, ratio / cfg.blurMaxAt);
            pb.SetFloat(PropConsts.blurRadius, b);
            sr.SetPropertyBlock(pb);

            if (time > cfg.ttl) {
                PooledDone();
            }
        }
    }
}
}
