using UnityEngine;

public class CutinGhost : MonoBehaviour {
    /*
     * 
            _Scale("Scale", Float) = 1.0
            _AIBS("Blur Step", Range(0.0001, 0.1)) = 0.1
            _BlurRad("Blur Radius", Float)  = 1.0
     * */
    public float ttl;
    private float time;
    public Color StartColor;
    public Color EndColor;
    public Vector2 scale;
    public Vector2 blurRadius;
    public float blurMaxAt = 0.5f;
    private Vector3 velocity;
    private Transform tr;
    private MaterialPropertyBlock pb;
    private SpriteRenderer sr;

    private void Awake() {
        tr = transform;
        pb = new MaterialPropertyBlock();
        sr = GetComponent<SpriteRenderer>();
        pb.SetTexture(PropConsts.mainTex, sr.sprite.texture);
    }
    public void Initialize(Vector2 location, Vector2 vel) {
        tr.position = location;
        this.velocity = vel;
        Update();
    }

    // Update is called once per frame
    private void Update() {
        time += ETime.dT;
        tr.localPosition += velocity * ETime.dT;
        float ratio = time / ttl;
        Color c = Color.Lerp(StartColor, EndColor, ratio);
        c.a *= 1f - ratio;
        sr.color = c;
        float s = Mathf.Lerp(scale.x, scale.y, ratio);
        tr.localScale = new Vector3(s, s, 1.0f);
        float b = Mathf.Lerp(blurRadius.x, blurRadius.y, ratio / blurMaxAt);
        pb.SetFloat(PropConsts.blurRadius, b);
        sr.SetPropertyBlock(pb);

        if (time > ttl) {
            Destroy(gameObject);
        }
    }
}
