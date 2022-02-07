using Danmokou.Behavior;
using Danmokou.Behavior.Functions;
using Danmokou.Scriptables;
using UnityEngine;

namespace Danmokou.Player {
public class ShipController : BehaviorEntity {
    public SpriteRenderer ghostSource = null!;
    public Color meterDisplay;
    public Color meterDisplayInner;
    public Color meterDisplayShadow;

    public ParticleSystem.MinMaxGradient speedLineColor;

    public GameObject ghost = null!;
    public float ghostFadeTime;
    public int ghostFrequency;
    public EffectStrategy RespawnOnHitEffect = null!;
    public EffectStrategy RespawnAfterEffect = null!;
    public EffectStrategy OnPreHitEffect = null!;
    public EffectStrategy OnHitEffect = null!;
    public EffectStrategy GoldenAuraEffect = null!;


    public void MaybeDrawWitchTimeGhost(int frame) {
        if (frame % ghostFrequency == 0) {
            DrawGhost(ghostFadeTime);
        }
    }

    public void DrawGhost(float fadeTime) {
        Instantiate(ghost).GetComponent<Ghost>().Initialize(ghostSource.sprite, tr.position, fadeTime);
    }
}
}
