using System;
using DMK.Core;
using DMK.DMath;
using DMK.Reflection;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Data/Boss Configuration")]
public class BossConfig : ScriptableObject {
    public GameObject boss;
    /// <summary>
    /// For display on the Boss Practice screen, eg. "Yukari (Ex)"
    /// </summary>
    public LocalizedString BossPracticeName;
    /// <summary>
    /// Eg. in a challenge screen string, "Kurokoma Card 1". Defaults to BossPracticeName.
    /// </summary>
    public LocalizedString challengeNameOverride;
    public LocalizedString ChallengeName => challengeNameOverride.Or(BossPracticeName);
    /// <summary>
    /// For display in replay descriptions. Defaults to BossPracticeName.
    /// </summary>
    public LocalizedString replayNameOverride;
    public LocalizedString ReplayName => replayNameOverride.Or(BossPracticeName);
    /// <summary>
    /// For display in the bottom margin when the boss in on screen. Defaults to the Japanese value of BossPracticeName.
    /// </summary>
    public LocalizedString bottomTrackerNameOverride;
    public LocalizedString BottomTrackerName => bottomTrackerNameOverride.OrSame(BossPracticeName.jp);
    public TextAsset stateMachine;
    /// <summary>
    /// For invocation in scripts, eg. "simp.mima"
    /// </summary>
    public string key;
    public BossColorScheme colors;

    /// <summary>
    /// This is a DELTA applied to the card circle euler angles every frame.
    /// </summary>
    public string cardRotator;
    public TP3 CardRotator =>
        ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(cardRotator) ? defaultCardRotator : cardRotator);
    private const string defaultCardRotator = "pxyz(dsine(9, 40, t), dsine(9p, 40, t)," +
                                              " lerpback(10, 14, 20, 24, mod(24, t), 90, -150))";

    /// <summary>
    /// This is a DELTA applied to the spell circle euler angles every frame.
    /// </summary>
    public string spellRotator;
    public TP3 SpellRotator =>
        ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(spellRotator) ? defaultSpellRotator : spellRotator);
    private const string defaultSpellRotator = "pxyz(0,0,lerpback(3, 4, 7, 8, mod(8, t), -220, -160))";

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
}