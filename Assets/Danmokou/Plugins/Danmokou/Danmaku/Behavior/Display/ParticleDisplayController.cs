using Danmokou.DMath;
using UnityEngine;

namespace Danmokou.Behavior.Display {
public class ParticleDisplayController : DisplayController {
    public ParticleSystem particles = null!;

    private ParticleSystem.MinMaxGradient defaultGrad;

    public float colorFromPalette = 0.7f;

    public override void SetMaterial(Material mat) {
        throw new System.NotImplementedException();
    }

    private bool defaultsLoaded;

    private void LoadDefaults() {
        if (defaultsLoaded) return;
        defaultGrad = particles.main.startColor;
        defaultsLoaded = true;
    }

    protected override void Awake() {
        base.Awake();
        LoadDefaults();
    }

    public override void UpdateStyle(BehaviorEntity.BEHStyleMetadata style) {
        LoadDefaults(); //This may be called before Awake through BEH.Awake
        var m = particles.main;
        if (style.recolor?.palette != null && colorFromPalette >= 0) {
            m.startColor =
                new ParticleSystem.MinMaxGradient(style.recolor.palette.Gradient.Evaluate(colorFromPalette)
                    .WithA(defaultGrad.color.a));
        } else {
            m.startColor = defaultGrad;
        }
    }

    public override void Show() {
        particles.Play();
    }

    public override void Hide() {
        particles.Stop();
    }

    public override MaterialPropertyBlock CreatePB() {
        return new MaterialPropertyBlock();
    }
}
}
