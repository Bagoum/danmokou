using System;
using System.Collections;
using System.Linq.Expressions;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Behavior.Functions;
using Danmokou.Core;
using Danmokou.Dialogue;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Scriptables;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using static Danmokou.DMath.LocationHelpers;

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
