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
    /// For display in the boss profile in the bottom left,
    /// eg. "Kurokoma&lt;br&gt;Saki"
    /// </summary>
    public string displayName;
    /// <summary>
    /// For display in the boss profile in the bottom left,
    /// eg. "Purple Haze&lt;br&gt;Lurking Beyond&lt;br&gt;The Pale"
    /// </summary>
    public string displayTitle;
    /// <summary>
    /// For display in the tracker in the bottom gutter, eg. "黒駒"
    /// </summary>
    public string trackName;
    public BossColorScheme colors;

    public string rotator;
    public BPY Rotator => ReflWrap<BPY>.Wrap(string.IsNullOrWhiteSpace(rotator) ? defaultRotator : rotator);
    private const string defaultRotator = "lerpback(10, 14, 20, 24, mod(24, t), 90, -200)";
    
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