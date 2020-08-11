using System;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;

/// <summary>
/// Provides centralized scene information to teleporter objects.
/// </summary>

[CreateAssetMenu(menuName = "Data/Boss Configuration")]
public class BossConfig : ScriptableObject {
    public GameObject boss;
    public string CardPracticeName;
    public TextAsset stateMachine;
    public string key;
    public string displayName;
    public string displayTitle;
    public string trackName;
    public Color uiColor;
    public Color uiHPColor;
    public Color cardColorR;
    public Color cardColorG;
    public Color cardColorB;
    public Color spellColor1;
    public Color spellColor2;
    public Color spellColor3;
    public string rotator;
    public BPY Rotator => (string.IsNullOrWhiteSpace(rotator) ? defaultRotator : rotator).Into<BPY>();
    private const string defaultRotator = "lerpback 10 14 20 24 (mod 24 t) 90 -200";

    [Serializable]
    public struct ProfileRender {
        public Texture2D image;
        public float offsetX;
        public float offsetY;
        public float zoom;
    }
    public ProfileRender profile;
    [CanBeNull] public GameObject defaultNonBG;
    [CanBeNull] public GameObject defaultSpellBG;
    [CanBeNull] public SOBgTransition defaultIntoSpellTransition;
    [CanBeNull] public SOBgTransition defaultIntoNonTransition;
    [CanBeNull] public GameObject bossCutin;
    public GameObject[] spellCutins;
    public SOBgTransition bossCutinTrIn => ResourceManager.WipeTex1;
    public SOBgTransition bossCutinTrOut => ResourceManager.WipeTex1;
    public GameObject bossCutinBg => ResourceManager.BlackBG;
    public float bossCutinTime => 5.6f;

    [CanBeNull]
    public GameObject Background(PhaseType pt) => pt.IsSpell() ? defaultSpellBG : defaultNonBG;
    [CanBeNull]
    public SOBgTransition IntoTransition(PhaseType pt) => 
        pt.IsSpell() ? defaultIntoSpellTransition : defaultIntoNonTransition;
}