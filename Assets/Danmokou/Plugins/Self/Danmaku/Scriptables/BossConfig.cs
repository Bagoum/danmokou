using System;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using static Danmaku.Enums;


[CreateAssetMenu(menuName = "Data/Boss Configuration")]
public class BossConfig : ScriptableObject {
    public GameObject boss;
    /// <summary>
    /// For display on the Boss Practice screen, eg. "Yukari (Ex)"
    /// </summary>
    public string CardPracticeName;
    /// <summary>
    /// For display in replay titles. Will use CardPracticeName if not provided.
    /// </summary>
    public string replayNameOverride;
    public string ReplayName => string.IsNullOrWhiteSpace(replayNameOverride) ? CardPracticeName : replayNameOverride;
    public TextAsset stateMachine;
    /// <summary>
    /// For invocation in scripts, eg. "simp.mima"
    /// </summary>
    public string key;
    /// <summary>
    /// Eg. in a challenge screen string, "Kurokoma Card 1"
    /// </summary>
    public string casualName;
    public string casualNameJP;
    public string CasualName => SaveData.s.Locale == Locale.JP ? casualNameJP : casualName;
    /// <summary>
    /// For display in the tracker in the bottom gutter, eg. "黒駒"
    /// </summary>
    public string trackName;
    public BossColorScheme colors;

    /// <summary>
    /// This is a delta applied to the card circle Z rotation every frame
    /// </summary>
    public string rotator;
    public BPY Rotator => ReflWrap<BPY>.Wrap(string.IsNullOrWhiteSpace(rotator) ? defaultRotator : rotator);
    private const string defaultRotator = "lerpback(10, 14, 20, 24, mod(24, t), 90, -200)";

    /// <summary>
    /// This is a Vector3 to which the spell circle eulerAngles is set to every frame
    /// </summary>
    public string spellRotator;
    public TP3 SpellRotator =>
        ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(spellRotator) ? defaultSpellRotator : spellRotator);
    private const string defaultSpellRotator = "pxyz(0,0,0)";//"pxyz(sine(9, 30, t), sine(9p, 30, t), 20 * t)";
    
    [Serializable]
    public class ProfileRender {
        public Texture2D leftSidebar;
        public Texture2D rightSidebar;
    }
    public ProfileRender profile;
    [CanBeNull] public GameObject defaultNonBG;
    [CanBeNull] public GameObject defaultSpellBG;
    [CanBeNull] public SOBgTransition defaultIntoSpellTransition;
    [CanBeNull] public SOBgTransition defaultIntoNonTransition;
    [CanBeNull] public GameObject bossCutin;
    public GameObject[] spellCutins;
    public SOBgTransition bossCutinTrIn => ResourceManager.WipeTex1;
    public float bossCutinBgTime => 2.0f;
    public SOBgTransition bossCutinTrOut => ResourceManager.Instantaneous;
    public GameObject bossCutinBg => ResourceManager.BlackBG;
    public float bossCutinTime => 4.8f;

    [CanBeNull]
    public GameObject Background(PhaseType pt) => pt.IsSpell() ? defaultSpellBG : defaultNonBG;
    [CanBeNull]
    public SOBgTransition IntoTransition(PhaseType pt) => 
        pt.IsSpell() ? defaultIntoSpellTransition : defaultIntoNonTransition;
}