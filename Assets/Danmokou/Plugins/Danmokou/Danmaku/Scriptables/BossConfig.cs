using System;
using BagoumLib.Culture;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Scriptables {
[CreateAssetMenu(menuName = "Data/Boss Configuration")]
public class BossConfig : ScriptableObject {
    public GameObject boss = null!;
    /// <summary>
    /// Eg. `simp.boss.mima`. If this is present, then the LSRs below can autocollect their values from the
    /// centralized string repository as `KEY` for BossPracticeName, `KEY.challenge` for ChallengeName,
    /// `KEY.replay` for ReplayName, `KEY.tracker` for BottomTrackerName.
    /// </summary>
    public string? localizationBaseKey;

    private LString? FromBaseKey(string suffix) {
        return string.IsNullOrWhiteSpace(localizationBaseKey) ?
            null :
            LocalizedStrings.TryFindReference(localizationBaseKey + suffix);
    }
    
    /// <summary>
    /// For display in naming situations where suffixes such as -1 or (Ex) are unacceptable.
    /// </summary>
    public LocalizedStringReference m_casualName = null!;
    public LString CasualName => 
        m_casualName.SetDefault(FromBaseKey(""));
    /// <summary>
    /// For display on the Boss Practice screen, eg. "Yukari (Ex)". Defaults to CasualName.
    /// </summary>
    public LocalizedStringReference m_bossPracticeName = null!;
    public LString BossPracticeName => 
        m_bossPracticeName.SetDefaultOrEmpty(FromBaseKey(".practice")).Or(CasualName);
    /// <summary>
    /// For display in replay descriptions. Defaults to BossPracticeName.
    /// </summary>
    public LocalizedStringReference replayNameOverride = null!;
    public LString ReplayName => 
        replayNameOverride.SetDefaultOrEmpty(FromBaseKey(".replay")).Or(CasualName);
    /// <summary>
    /// For display in the bottom margin when the boss in on screen. Defaults to BossPracticeName.
    /// </summary>
    public LocalizedStringReference bottomTrackerNameOverride = null!;
    public LString BottomTrackerName => 
        bottomTrackerNameOverride.SetDefaultOrEmpty(FromBaseKey(".tracker")).Or(CasualName);
    
    public TextAsset stateMachine = null!;
    /// <summary>
    /// For invocation in scripts, eg. "simp.mima"
    /// </summary>
    public string key = null!;
    public BossColorScheme colors = null!;

    /// <summary>
    /// This is a DELTA applied to the card circle euler angles every frame.
    /// </summary>
    public string? cardRotator;
    [ReflectInto]
    public TP3 CardRotator =>
        ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(cardRotator) ? defaultCardRotator : cardRotator!);
    private const string defaultCardRotator = "pxyz(dsine(9, 40, t), dsine(9p, 40, t)," +
                                              " lerpback(10, 14, 20, 24, mod(24, t), 90, -150))";

    /// <summary>
    /// This is a DELTA applied to the spell circle euler angles every frame.
    /// </summary>
    public string? spellRotator;
    [ReflectInto]
    public TP3 SpellRotator =>
        ReflWrap<TP3>.Wrap(string.IsNullOrWhiteSpace(spellRotator) ? defaultSpellRotator : spellRotator!);
    private const string defaultSpellRotator = "pxyz(0,0,lerpback(3, 4, 7, 8, mod(8, t), -220, -160))";

    [Serializable]
    public class ProfileRender {
        public Texture2D leftSidebar = null!;
        public Texture2D rightSidebar = null!;
    }

    public ProfileRender profile = null!;
    public GameObject? defaultNonBG;
    public GameObject? defaultSpellBG;
    public SOBgTransition? defaultIntoSpellTransition;
    public SOBgTransition? defaultIntoNonTransition;
    public GameObject? bossCutin;
    public GameObject[] spellCutins = null!;
    public SOBgTransition bossCutinTrIn => ResourceManager.WipeTex1;
    public float bossCutinBgTime => 2.0f;
    public SOBgTransition bossCutinTrOut => ResourceManager.Instantaneous;
    public GameObject bossCutinBg => ResourceManager.BlackBG;
    public float bossCutinTime => 4.8f;

    public GameObject? Background(PhaseType pt) => pt.IsSpell() ? defaultSpellBG : defaultNonBG;

    public SOBgTransition? IntoTransition(PhaseType pt) =>
        pt.IsSpell() ? defaultIntoSpellTransition : defaultIntoNonTransition;
}
}