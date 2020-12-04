using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TMPro;
using System.Threading;
using Core;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using static GameManagement;
using static Danmaku.Enums;

[Serializable]
public struct PrioritySprite {
    public int priority;
    public SpriteRenderer sprite;
}

public interface IUIManager {
    void SetBossHPLoader([CanBeNull] Enemy boss);
    void CloseBoss();
    void CloseProfile();
    void AddProfile(BossConfig.ProfileRender render);
    void SwitchProfile(BossConfig.ProfileRender render);
    void SetBossColor(Color textColor, Color bossHPColor);
    BottomTracker TrackBEH(BehaviorEntity beh, string title, ICancellee cT);
    void ShowBossLives(int bossLives);
    void ShowStaticTimeout(float maxTime);
    void DoTimeout(bool withSound, float maxTime, ICancellee cT, float? stayOnZero = null);
    void ShowPhaseType(PhaseType? phase);
    void SetSpellname([CanBeNull] string title);
}
public class UIManager : RegularUpdater, IUIManager {
    static UIManager() {
        GameStateManager.UnpauseAnimator = SlideUnpause;
    }

    private static UIManager main;
    public bool autoShiftCamera;
    [FormerlySerializedAs("camera")] public Camera uiCamera;
    public XMLPauseMenu PauseManager;
    public static XMLPauseMenu PauseMenu => main.PauseManager;
    public XMLDeathMenu DeathManager;
    public XMLPracticeSuccessMenu PracticeSuccessMenu;
    public UIBuilderRenderer uiRenderer;
    public TextMeshPro spellnameText;
    public TextMeshPro timeout;
    public TextMeshPro difficulty;
    public TextMeshPro score;
    public TextMeshPro maxScore;
    public GameObject scoreExtend_parent;
    public TextMeshPro scoreExtend;
    public TextMeshPro pivMult;
    public TextMeshPro lifePoints;
    public TextMeshPro graze;
    public TextMeshPro power;
    public TextMeshPro message;
    public TextMeshPro multishotIndicator;
    public TextMeshPro centerMessage;
    private const string deathCounterFormat = "死{0:D2}";
    private const string timeoutTextFormat = "<mspace=4.3>{0:F1}</mspace>";
    private const string fpsFormat = "FPS: <mspace=1.5>{0:F0}</mspace>";
    [CanBeNull] private Coroutine timeoutCor;
    private static readonly int ValueID = Shader.PropertyToID("_Value");
    [CanBeNull] private Coroutine spellnameController;

    private Color spellColor;
    private Color spellColorTransparent;
    public float spellnameFadeIn = 1f;

    private float profileTime = 0f;
    public SpriteRenderer PIVDecayBar;
    public SpriteRenderer MeterBar;
    private Color defaultMeterColor;
    private Color defaultMeterColor2;
    public SpriteRenderer BossHPBar;
    private MaterialPropertyBlock pivDecayPB;
    private MaterialPropertyBlock meterPB;
    private MaterialPropertyBlock bossHPPB;
    private MaterialPropertyBlock leftSidebarPB;
    private MaterialPropertyBlock rightSidebarPB;

    public GameObject trackerPrefab;

    private void Awake() {
        main = this;
        spellColor = spellnameText.color;
        spellColor.a = 1;
        spellColorTransparent = spellColor;
        spellColorTransparent.a = 0;
        PIVDecayBar.GetPropertyBlock(pivDecayPB = new MaterialPropertyBlock());
        MeterBar.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.threshold, (float)CampaignData.meterUseThreshold);
        defaultMeterColor = MeterBar.sharedMaterial.GetColor(PropConsts.fillColor);
        defaultMeterColor2 = MeterBar.sharedMaterial.GetColor(PropConsts.fillColor2);
        BossHPBar.GetPropertyBlock(bossHPPB = new MaterialPropertyBlock());
        leftSidebar.GetPropertyBlock(leftSidebarPB = new MaterialPropertyBlock());
        rightSidebar.GetPropertyBlock(rightSidebarPB = new MaterialPropertyBlock());
        timeout.text = "";
        spellnameText.text = "";
        message.text = centerMessage.text = "";
        multishotIndicator.text = campaign.MultishotString;
        challengeHeader.text = challengeText.text = "";
        UpdateTags();
        ShowBossLives(0);
        stackedProfiles.Push(defaultProfile);
        SetProfile(defaultProfile);
        SetBossHPLoader(null);
        if (autoShiftCamera) uiCamera.transform.localPosition = -References.bounds.center;
    }
    
    protected override void BindListeners() {
        base.BindListeners();
        Listen(Events.GameStateHasChanged, HandleGameStateChange);
        Listen(Events.CampaignDataHasChanged, () => updateAllUI = true);
        Listen(CampaignData.ItemExtendAcquired, LifeExtendItems);
        Listen(CampaignData.ScoreExtendAcquired, LifeExtendScore);
        Listen(CampaignData.PhaseCompleted, PhaseCompleted);
        Listen(PlayerInput.MeterIsActive, SetMeterActivated);
        Listen(PlayerInput.PlayerDeactivatedMeter, UnSetMeterActivated);
        RegisterDI<IUIManager>(this);
    }
    
    public static float MenuRightOffset =>
        MainCamera.ResourcePPU * (MainCamera.HorizRadius - References.bounds.right - References.bounds.center.x);

    private void Start() {
        UpdatePB();
        _UpdatePlayerUI();
        GameStateManager.PauseAllowed = PauseManager.UI != null;
    }

    public static void UpdateTags() {
        main.difficulty.text = GameManagement.Difficulty.Describe();
    }

    [CanBeNull] private Enemy bossHP;

    public void SetBossHPLoader([CanBeNull] Enemy boss) {
        bossHP = boss;
        BossHPBar.enabled = boss != null;
        if (boss != null) {
            bossHPPB.SetColor(PropConsts.fillColor2, bossHP.unfilledColor);
            bossHPPB.SetColor(PropConsts.unfillColor, bossHP.unfilledColor);
        }
    }

    private void SetMeterActivated(Color c) {
        c.a = 1;
        meterPB.SetColor(PropConsts.fillColor, c);
        meterPB.SetColor(PropConsts.fillColor2, c);
    }
    private void UnSetMeterActivated() {
        meterPB.SetColor(PropConsts.fillColor, defaultMeterColor);
        meterPB.SetColor(PropConsts.fillColor2, defaultMeterColor2);
    }
    private void UpdatePB() {
        //pivDecayPB.SetFloat(PropConsts.time, time);
        pivDecayPB.SetFloat(PropConsts.fillRatio, (float)campaign.Faith);
        pivDecayPB.SetFloat(PropConsts.innerFillRatio, Mathf.Clamp01((float)campaign.UIVisibleFaithDecayLenienceRatio));
        PIVDecayBar.SetPropertyBlock(pivDecayPB);
        meterPB.SetFloat(PropConsts.fillRatio, (float) campaign.Meter);
        MeterBar.SetPropertyBlock(meterPB);
        //bossHPPB.SetFloat(PropConsts.time, time);
        if (bossHP != null) {
            main.bossHPPB.SetColor(PropConsts.fillColor, bossHP.UIHPColor);
            bossHPPB.SetFloat(PropConsts.fillRatio, Dialoguer.DialogueActive ? 0 : bossHP.DisplayBarRatio);
        }
        BossHPBar.SetPropertyBlock(bossHPPB);
        leftSidebarPB.SetFloat(PropConsts.time, profileTime);
        rightSidebarPB.SetFloat(PropConsts.time, profileTime);
        leftSidebar.SetPropertyBlock(leftSidebarPB);
        rightSidebar.SetPropertyBlock(rightSidebarPB);
    }

    public TextMeshPro fps;
    private const int fpsSmooth = 10;
    private int fpsUpdateCounter = fpsSmooth;
    private float accdT = 0f;

    private bool updateAllUI = false;
    private void Update() {
        profileTime += ETime.dT;
        accdT += Time.unscaledDeltaTime;
        if (--fpsUpdateCounter == 0) {
            fps.text = string.Format(fpsFormat, fpsSmooth / accdT);
            fpsUpdateCounter = fpsSmooth;
            accdT = 0;
        }
        UpdatePB();
        if (updateAllUI) _UpdatePlayerUI();
        updateAllUI = false;
    }

    public override void RegularUpdate() { }

    public void ShowStaticTimeout(float maxTime) {
        EndTimeout();
        timeout.text = (maxTime < float.Epsilon) ? "" : string.Format(timeoutTextFormat, maxTime);
    }
    public void DoTimeout(bool withSound, float maxTime, ICancellee cT, float? stayOnZero = null) {
        EndTimeout();
        if (maxTime < float.Epsilon) {
            timeout.text = "";
        } else {
            timeoutCor = StartCoroutine(Timeout(maxTime, withSound, stayOnZero ?? 2f, cT));
        }
    }
    public void EndTimeout() {
        if (timeoutCor != null) {
            StopCoroutine(timeoutCor);
            timeoutCor = null;
        }
        timeout.text = "";
    }

    public SFXConfig[] countdownSounds;
    private IEnumerator Timeout(float maxTime, bool withSound, float stayOnZero, ICancellee cT) {
        float currTime = maxTime;
        var currTimeIdent = -2;
        while (currTime > 0) {
            if (Mathf.RoundToInt(currTime * 10) != currTimeIdent) {
                timeout.text = string.Format(timeoutTextFormat, currTime);
                currTimeIdent = Mathf.RoundToInt(currTime * 10);
            }
            yield return null;
            if (cT.Cancelled) { break; }
            var tryCross = Mathf.FloorToInt(currTime);
            currTime -= ETime.dT;
            if (withSound && currTime < tryCross) {
                if (0 < tryCross && tryCross <= countdownSounds.Length) {
                    SFXService.Request(countdownSounds[tryCross - 1]);
                }
            }
        }
        if (currTime < 1) {
            timeout.text = string.Format(timeoutTextFormat, 0);
        }
        while (stayOnZero > 0f) {
            yield return null;
            stayOnZero -= ETime.dT;
        }
        timeout.text = "";
    }

    private IEnumerator FadeSpellname(float fit, Color from, Color to) {
        spellnameText.color = from;
        for (float t = 0; t < fit; t += ETime.dT) {
            yield return null;
            spellnameText.color = Color.LerpUnclamped(from, to, t / fit);
        }
        spellnameText.color  = to;
    }

    public void SetSpellname([CanBeNull] string title) {
        spellnameText.text = title ?? "";
        if (spellnameController != null) StopCoroutine(spellnameController);
        spellnameController = StartCoroutine(FadeSpellname(spellnameFadeIn, spellColorTransparent, spellColor));
    }

    public Material bossColorizer;

    public SpriteRenderer leftSidebar;
    public SpriteRenderer rightSidebar;
    public BossConfig.ProfileRender defaultProfile;
    private readonly Stack<BossConfig.ProfileRender> stackedProfiles = new Stack<BossConfig.ProfileRender>();

    public void CloseProfile() {
        var src = stackedProfiles.Pop();
        SetProfile(stackedProfiles.Peek(), src);
    }

    public void AddProfile(BossConfig.ProfileRender render) {
        SetProfile(render, stackedProfiles.Peek());
        stackedProfiles.Push(render);
    }

    public void SwitchProfile(BossConfig.ProfileRender render) {
        if (render == stackedProfiles.Peek()) return;
        SetProfile(render, stackedProfiles.Pop());
        stackedProfiles.Push(render);
    }

    private void SetProfile(BossConfig.ProfileRender target, BossConfig.ProfileRender source = null) {
        var src = source ?? defaultProfile;
        profileTime = 0;
        leftSidebarPB.SetTexture(PropConsts.fromTex, src.leftSidebar.Elvis(defaultProfile.leftSidebar));
        rightSidebarPB.SetTexture(PropConsts.fromTex, src.rightSidebar.Elvis(defaultProfile.rightSidebar));
        leftSidebarPB.SetTexture(PropConsts.toTex, target.leftSidebar.Elvis(defaultProfile.leftSidebar));
        rightSidebarPB.SetTexture(PropConsts.toTex, target.rightSidebar.Elvis(defaultProfile.rightSidebar));
    }

    /// <summary>
    /// (int, Sprite) where int is the number of "boss lives" required to show the sprite
    /// </summary>
    public PrioritySprite[] bossHealthSprites;
    public void SetBossColor(Color textColor, Color bossHPColor) {
        bossColorizer.SetMaterialOutline(textColor);
        foreach (var p in bossHealthSprites) {
            p.sprite.color = bossHPColor;
        }
    }

    public void ShowBossLives(int bossLives) {
        foreach (var p in bossHealthSprites) {
            p.sprite.enabled = p.priority <= bossLives;
        }
    }
    public void CloseBoss() {
        SetSpellname(null);
        ShowBossLives(0);
        SetBossHPLoader(null);
    }

    public TextMeshPro phaseDescription;
    public void ShowPhaseType(PhaseType? phase) {
        void Set(string s) => phaseDescription.text = s;
        if (phase.Try(out var p)) {
            if (p == PhaseType.NONSPELL) Set("NON");
            else if (p == PhaseType.SPELL) Set("SPELL");
            else if (p == PhaseType.TIMEOUT) Set("SURVIVAL");
            else if (p == PhaseType.FINAL) Set("FINAL");
            else if (p == PhaseType.STAGE) Set("STAGE");
            else if (p.IsStageBoss()) Set("CHALLENGER\nAPPROACHING");
            else if (p == PhaseType.DIALOGUE) {}
            else Set("");
        }
    }

    private static readonly Vector2 slideFrom = new Vector2(5, 0);
    private void SlideInUI() {
        uiRenderer.MoveToNormal();
        uiRenderer.Slide(slideFrom, Vector2.zero, 0.3f, DMath.M.EOutSine, success => {
            if (success) uiRenderer.MoveToFront();
        });
    }

    public static void SlideUnpause(Action onDone) {
        main.uiRenderer.MoveToNormal();
        main.uiRenderer.Slide(null, slideFrom, 0.3f, x => x, _ => onDone());
    }
    
    private void HandleGameStateChange(GameState state) {
        if (state == GameState.RUN) {
            PauseManager.HideOptions(true);
            DeathManager.HideMe();
            PracticeSuccessMenu.HideMe();
        } else if (state == GameState.PAUSE) {
            PauseManager.ShowOptions();
            SlideInUI();
        } else if (state == GameState.DEATH) {
            DeathManager.ShowMe();
            SlideInUI();
        } else if (state == GameState.SUCCESS) {
            PracticeSuccessMenu.ShowMe();
            SlideInUI();
        }
    }

    public SpriteRenderer[] healthPoints;
    public SpriteRenderer[] bombPoints;
    public Sprite healthEmpty;
    public Sprite[] healthItrs;
    public Sprite[] bombItrs;
    private const string pivMultFormat = "x<mspace=1.53>{0:00.00}</mspace>";
    private const string lifePointsFormat = "<mspace=1.5>{0}/{1}</mspace>";
    private const string grazeFormat = "<mspace=1.5>{0}</mspace>";
    private const string powerFormat = "<mspace=1.5>{0:F2}/{1:F2}</mspace>";
    private const string scoreFormat = "<mspace=1.7>{0}</mspace>";

    private static string ToMonospaceThousands(long val, float mspace=1.7f) {
        string ms = $"<mspace={mspace}>";
        string msc = "</mspace>";
        StringBuilder sb = new StringBuilder();
        int pow = 0;
        for (; Math.Pow(10, pow + 3) < val; pow += 3) {}
        for (bool first = true; pow >= 0; pow -= 3, first=false) {
            if (!first) sb.Append(',');
            int places = (int) Math.Floor(val / Math.Pow(10, pow));
            sb.Append(ms);
            if (first) sb.Append(places);
            else sb.Append(places.ToString().PadLeft(3, '0'));
            sb.Append(msc);
            val -= (long) (places * Math.Pow(10, pow));
        }
        return sb.ToString();
    }
    private void _UpdatePlayerUI() {
        multishotIndicator.text = campaign.MultishotString;
        if (campaign.NextScoreLife.Try(out var scoreExt)) {
            scoreExtend_parent.SetActive(true);
            scoreExtend.text = string.Format(scoreFormat, scoreExt);
        } else {
            scoreExtend_parent.SetActive(false);
        }
        score.text = string.Format(scoreFormat, campaign.UIVisibleScore);
        maxScore.text = string.Format(scoreFormat, campaign.MaxScore);
        pivMult.text = string.Format(pivMultFormat, campaign.PIV);
        lifePoints.text = string.Format(lifePointsFormat, campaign.LifeItems, campaign.NextLifeItems);
        graze.text = string.Format(grazeFormat, campaign.Graze);
        power.text = string.Format(powerFormat, campaign.Power, CampaignData.powerMax);
        for (int ii = 0; ii < healthPoints.Length; ++ii) healthPoints[ii].sprite = healthEmpty;
        for (int hi = 0; hi < healthItrs.Length; ++hi) {
            for (int ii = 0; ii + hi * healthPoints.Length < campaign.Lives && ii < healthPoints.Length; ++ii) {
                healthPoints[ii].sprite = healthItrs[hi];
            }
        }
        for (int ii = 0; ii < bombPoints.Length; ++ii) bombPoints[ii].sprite = healthEmpty;
        for (int bi = 0; bi < bombItrs.Length; ++bi) {
            for (int ii = 0; ii + bi * bombPoints.Length < campaign.Bombs && ii < bombPoints.Length; ++ii) {
                bombPoints[ii].sprite = bombItrs[bi];
            }
        }
    }

    private IEnumerator FadeMessage(string msg, ICancellee cT, float timeIn = 1f, float timeStay = 4f,
        float timeOut = 1f) {
        message.text = msg;
        return FadeSprite(message.color, c => message.color = c, timeIn, timeStay, timeOut, cT);
    }
    private IEnumerator FadeMessageCenter(string msg, ICancellee cT, out float totalTime, 
        float timeIn = 0.2f, float timeStay = 0.8f, float timeOut = 0.3f) {
        centerMessage.text = msg;
        totalTime = timeIn + timeStay + timeOut;
        return FadeSprite(centerMessage.color, c => centerMessage.color = c, timeIn, timeStay, timeOut, cT);
    }

    private static IEnumerator FadeSprite(Color c, Action<Color> apply, float timeIn, float timeStay,
        float timeOut, ICancellee cT) {
        for (float t = 0; t < timeIn; t += ETime.dT) {
            c.a = t / timeIn;
            apply(c);
            if (cT.Cancelled) break;
            yield return null;
        }
        c.a = 1;
        apply(c);
        for (float t = 0; t < timeStay; t += ETime.dT) {
            if (cT.Cancelled) break;
            yield return null;
        }
        for (float t = 0; t < timeOut; t += ETime.dT) {
            c.a = 1 - t / timeOut;
            apply(c);
            if (cT.Cancelled) break;
            yield return null;
        }
        c.a = 0;
        apply(c);
    }

    [CanBeNull] private static Cancellable messageFadeToken;

    private void _Message(string msg) {
        messageFadeToken?.Cancel();
        StartCoroutine(FadeMessage(msg, messageFadeToken = new Cancellable()));
    }
    [CanBeNull] private static Cancellable cmessageFadeToken;

    private void _CMessage(string msg, out float totalTime) {
        cmessageFadeToken?.Cancel();
        StartCoroutine(FadeMessageCenter(msg, cmessageFadeToken = new Cancellable(), out totalTime));
    }

    public static void MessageChallengeEnd(bool success, out float totalTime) => main._CMessage(
        success ?
            "Challenge Pass!" :
            "Challenge Fail..."
        , out totalTime);

    private void Message(string msg) => _Message(msg);
    private void LifeExtendScore() => Message("Score Extend Acquired!");
    private void LifeExtendItems() => Message("Life Item Extend Acquired!");
    private void PhaseCompleted(PhaseCompletion pc) {
        if (pc.Captured.HasValue) {
            Message(pc.Captured.Value
                ? "Card Captured!"
                : "Card Capture Failed..."
                );
        }
    }


    public BottomTracker TrackBEH(BehaviorEntity beh, string title, ICancellee cT) => 
        Instantiate(trackerPrefab).GetComponent<BottomTracker>().Initialize(beh, title, cT);

    public SpriteRenderer stageAnnouncer;
    public TextMeshPro stageDeannouncer;

    private const float stageAnnounceIn = 1f;
    private const float stageAnnounceStay = 3f;
    private const float stageAnnounceOut = 1f;
    public static void AnnounceStage(ICancellee cT, out float time) {
        time = stageAnnounceIn + stageAnnounceOut + stageAnnounceStay;
        main.StartCoroutine(FadeSprite(Color.white, c => main.stageAnnouncer.color = c, stageAnnounceIn, stageAnnounceStay,
            stageAnnounceOut, cT));
    }
    private const float stageDAnnounceIn = 0.5f;
    private const float stageDAnnounceStay = 3f;
    private const float stageDAnnounceOut = 1f;
    public static void DeannounceStage(ICancellee cT, out float time) {
        time = stageDAnnounceIn + stageDAnnounceOut + stageDAnnounceStay;
        main.StartCoroutine(FadeSprite(Color.white, c => main.stageDeannouncer.color = c, stageDAnnounceIn, stageDAnnounceStay,
            stageDAnnounceOut, cT));
    }

    public TextMeshPro challengeHeader;
    public TextMeshPro challengeText;

    public static void RequestChallengeDisplay(PhaseChallengeRequest cr, GameMetadata meta) {
        main.challengeHeader.text = cr.phase.Title(meta);
        main.challengeText.text = cr.Description;
    }
}
