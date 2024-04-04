using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using UnityEngine;
using TMPro;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SM;
using Danmokou.UI.XML;
using UnityEngine.Serialization;
using static Danmokou.Services.GameManagement;
using static Danmokou.DMath.ColorHelpers;

namespace Danmokou.UI {
[Serializable]
public struct PrioritySprite {
    public int priority;
    public SpriteRenderer sprite;
}

public interface IUIManager {
    Camera Camera { get; }
    void SetBossHPLoader(Enemy? boss);
    void CloseBoss();
    void CloseProfile();
    void AddProfile(BossConfig.ProfileRender render);
    void SwitchProfile(BossConfig.ProfileRender render);
    void SetBossColor(Color textColor, Color bossHPColor);
    void TrackBEH(BehaviorEntity beh, string title, ICancellee cT);
    void ShowBossLives(int bossLives);
    void ShowStaticTimeout(float maxTime);
    void ShowTimeout(bool withSound, float maxTime, ICancellee cT, float? stayOnZero = null);
    void ShowPhaseType(PhaseType? phase);
    void SetSpellname(string? title, (int success, int total)? rate = null);

    void DisplayChallenge(PhaseChallengeRequest cr, SharedInstanceMetadata meta);
    void MessageChallengeEnd(bool success, out float totalTime);

}

public interface IStageAnnouncer {
    void AnnounceStage(ICancellee cT, out float time);
    void DeannounceStage(ICancellee cT, out float time);
}

public class UIManager : CoroutineRegularUpdater, IUIManager, IStageAnnouncer {

    public bool autoShiftCamera;
    [FormerlySerializedAs("camera")] public Camera uiCamera = null!;
    public Camera Camera => uiCamera;
    public XMLPauseMenu PauseManager = null!;
    public SpriteRenderer frame = null!;
    public TextMeshPro spellnameText = null!;
    public GameObject cardSuccessContainer = null!;
    public TextMeshPro cardSuccessText = null!;
    public TextMeshPro cardAttemptsText = null!;
    public TextMeshPro timeout = null!;
    public TextMeshPro difficulty = null!;
    public TextMeshPro score = null!;
    public TextMeshPro maxScore = null!;
    public GameObject scoreExtend_parent = null!;
    public TextMeshPro scoreExtend = null!;
    public TextMeshPro pivMult = null!;
    public TextMeshPro lifePoints = null!;
    public TextMeshPro graze = null!;
    public TextMeshPro power = null!;
    public TextMeshPro message = null!;
    public TextMeshPro multishotIndicator = null!;
    public TextMeshPro centerMessage = null!;
    public TextMeshPro rankLevel = null!;
    public SpriteRenderer rankPointBar = null!;
    public Gradient rankPointBarColor = null!;
    private const string deathCounterFormat = "死{0:D2}";
    private const string timeoutTextFormat = "<mspace=4.3>{0:F1}</mspace>";
    private const string fpsFormat = "FPS: <mspace=1.5>{0:F0}</mspace>";
    private Cancellable? timeoutCor;
    private static readonly int ValueID = Shader.PropertyToID("_Value");
    private Cancellable? spellnameController;

    private Color spellColor;
    private Color spellColorTransparent;
    public float spellnameFadeIn = 1f;

    private float profileTime = 0f;
    public SpriteRenderer PIVDecayBar = null!;
    public SpriteRenderer MeterBar = null!;
    private Color defaultMeterColor;
    private Color defaultMeterColor2;
    public SpriteRenderer BossHPBar = null!;
    private MaterialPropertyBlock pivDecayPB = null!;
    private MaterialPropertyBlock meterPB = null!;
    private MaterialPropertyBlock bossHPPB = null!;
    private MaterialPropertyBlock rankPB = null!;
    private MaterialPropertyBlock leftSidebarPB = null!;
    private MaterialPropertyBlock rightSidebarPB = null!;

    public GameObject? trackerPrefab;

    private void Awake() {
        spellColor = spellnameText.color;
        spellColor.a = 1;
        spellColorTransparent = spellColor;
        spellColorTransparent.a = 0;
        if (frame.sprite == null) frame.sprite = References.defaultUIFrame;
        PIVDecayBar.GetPropertyBlock(pivDecayPB = new MaterialPropertyBlock());
        MeterBar.GetPropertyBlock(meterPB = new MaterialPropertyBlock());
        meterPB.SetFloat(PropConsts.threshold, (float) Instance.MeterF.MeterUseThreshold);
        defaultMeterColor = MeterBar.sharedMaterial.GetColor(PropConsts.fillColor);
        defaultMeterColor2 = MeterBar.sharedMaterial.GetColor(PropConsts.fillColor2);
        BossHPBar.GetPropertyBlock(bossHPPB = new MaterialPropertyBlock());
        rankPointBar.GetPropertyBlock(rankPB = new MaterialPropertyBlock());
        leftSidebar.GetPropertyBlock(leftSidebarPB = new MaterialPropertyBlock());
        rightSidebar.GetPropertyBlock(rightSidebarPB = new MaterialPropertyBlock());
        timeout.text = "";
        spellnameText.text = "";
        cardSuccessContainer.SetActive(false);
        message.text = centerMessage.text = "";
        multishotIndicator.text = Instance.MultishotString;
        challengeHeader.text = challengeText.text = "";
        ShowBossLives(0);
        stackedProfiles.Push(defaultProfile);
        SetProfile(defaultProfile, defaultProfile);
        SetBossHPLoader(null);
        if (autoShiftCamera) 
            uiCamera.transform.localPosition = 
                new Vector3(-LocationHelpers.PlayableBounds.center.x, -LocationHelpers.PlayableBounds.center.y, 
                    uiCamera.transform.localPosition.z);
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IUIManager>(this);
        RegisterService<IStageAnnouncer>(this);
        
        Listen(PlayerController.MeterIsActive, SetMeterActivated);
        Listen(PlayerController.PlayerDeactivatedMeter, UnSetMeterActivated);
        
        Listen(EvInstance, i => i.ExtendAcquired, ext => {
            if (ext is ExtendType.SCORE) {
                LifeExtendScore();
            } else if (ext is ExtendType.LIFE_ITEM) {
                LifeExtendItems();
                UpdateLifeText();
            }
        });
        
        Listen(EvInstance, i => i.Graze, g => graze.text = string.Format(grazeFormat, g));
        Listen(EvInstance, i => i.ScoreF.VisibleScore, s => score.text = string.Format(scoreFormat, s));
        Listen(EvInstance, i => i.ScoreF.MaxScore, s => maxScore.text = string.Format(scoreFormat, s));
        Listen(EvInstance, i => i.ScoreF.Multiplier, p => pivMult.text = string.Format(pivMultFormat, p));
        Listen(EvInstance, i => i.PowerF.Power, p => power.text = string.Format(powerFormat, p, EvInstance.Value.PowerF.PowerMax));
        Listen(EvInstance, i => i.BasicF.Lives, l => {
            for (int ii = 0; ii < healthPoints.Length; ++ii) healthPoints[ii].sprite = healthEmpty;
            for (int hi = 0; hi < healthItrs.Length; ++hi) {
                for (int ii = 0; ii + hi * healthPoints.Length < Instance.BasicF.Lives && ii < healthPoints.Length; ++ii) {
                    healthPoints[ii].sprite = healthItrs[hi];
                }
            }
        });
        Listen(EvInstance, i => i.BasicF.Bombs, b => {
            var color = Instance.TeamCfg?.Support is Ability.Bomb { BombsRequired: not null } ?
                Color.white :
                new Color(0.5f, 0.5f, 0.5f, 0.9f);
            for (int ii = 0; ii < bombPoints.Length; ++ii) {
                bombPoints[ii].sprite = healthEmpty;
                bombPoints[ii].color = color;
            }
            for (int bi = 0; bi < bombItrs.Length; ++bi) {
                for (int ii = 0; ii + bi * bombPoints.Length < Instance.BasicF.Bombs && ii < bombPoints.Length; ++ii) {
                    bombPoints[ii].sprite = bombItrs[bi];
                }
            }
        });

        void UpdateLifeText() {
            lifePoints.text = string.Format(lifePointsFormat, Instance.LifeItemF.LifeItems.Value, Instance.LifeItemF.NextLifeItems);
        }
        UpdateLifeText();
        
        void UpdateTeamText() {
            multishotIndicator.text = Instance.MultishotString;
        }
        UpdateTeamText();
        
        void UpdateScoreExtendText() {
            if (Instance.ScoreExtendF.NextScoreLife.Value.Try(out var scoreExt)) {
                scoreExtend_parent.SetActive(true);
                scoreExtend.text = string.Format(scoreFormat, scoreExt);
            } else {
                scoreExtend_parent.SetActive(false);
            }
        }
        UpdateScoreExtendText();

        void UpdateRankText() {
            if (Instance.RankF is { } r) {
                rankLevel.text = $"Rank {r.RankLevel}";
            } else {
                rankLevel.text = "";
            }
        }
        UpdateRankText();

        void UpdateDifficultyText() {
            difficulty.text = GameManagement.Difficulty.Describe().ToLower();
        }
        UpdateDifficultyText();
        
        Listen(EvInstance, i => i.LifeItemF.LifeItems, _ => UpdateLifeText());
        Listen(EvInstance, i => i.TeamUpdated, UpdateTeamText);
        Listen(EvInstance, i => i.RankF.RankLevelChanged, _ => UpdateRankText());
        if (scoreExtend_parent != null)
            Listen(EvInstance, i => i.ScoreExtendF.NextScoreLife, _ => UpdateScoreExtendText());
        Listen(EvInstance, _ => UpdateDifficultyText());
    }

    private void Start() {
        UpdatePB();
    }

    private Enemy? bossHP;

    public void SetBossHPLoader(Enemy? boss) {
        bossHP = boss;
        BossHPBar.enabled = boss != null;
        if (boss != null) {
            bossHPPB.SetColor(PropConsts.fillColor2, bossHP!.unfilledColor);
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
        pivDecayPB.SetFloat(PropConsts.fillRatio, Instance.FaithF.VisibleFaith.Value);
        pivDecayPB.SetFloat(PropConsts.innerFillRatio, Mathf.Clamp01(Instance.FaithF.VisibleFaithLenience.Value));
        PIVDecayBar.SetPropertyBlock(pivDecayPB);
        meterPB.SetFloat(PropConsts.fillRatio, (float) Instance.MeterF.Meter);
        meterPB.SetColor(PropConsts.colorMult, Instance.TeamCfg?.Support is Ability.Metered ? Color.white : 
            new Color(0.5f, 0.5f, 0.5f, 0.7f));
        MeterBar.SetPropertyBlock(meterPB);
        //bossHPPB.SetFloat(PropConsts.time, time);
        if (bossHP != null) {
            bossHPPB.SetColor(PropConsts.fillColor, bossHP.UIHPColor);
            bossHPPB.SetFloat(PropConsts.fillRatio, bossHP.DisplayBarRatio);
        }
        BossHPBar.SetPropertyBlock(bossHPPB);
        rankPB.SetColor(PropConsts.fillColor, rankPointBarColor.Evaluate((float)(Instance.RankF?.RankRatio ?? 0)));
        rankPB.SetFloat(PropConsts.fillRatio, Instance.RankF?.VisibleRankPointFill.Value ?? 0);
        rankPointBar.SetPropertyBlock(rankPB);
        leftSidebarPB.SetFloat(PropConsts.time, profileTime);
        rightSidebarPB.SetFloat(PropConsts.time, profileTime);
        leftSidebar.SetPropertyBlock(leftSidebarPB);
        rightSidebar.SetPropertyBlock(rightSidebarPB);
    }

    public TextMeshPro fps = null!;
    private const int fpsSmooth = 10;
    private int fpsUpdateCounter = fpsSmooth;
    private float accdT = 0f;
    private float lastFps = -1;

    private void Update() {
        profileTime += ETime.dT;
        accdT += Time.unscaledDeltaTime;
        if (--fpsUpdateCounter == 0) {
            var nextFps = Mathf.RoundToInt(fpsSmooth / accdT);
            if (nextFps != lastFps) {
                fps.text = StringBuffer.FormatPooled(fpsFormat, lastFps = nextFps);
            }
            fpsUpdateCounter = fpsSmooth;
            accdT = 0;
        }
        UpdatePB();
    }

    public void ShowStaticTimeout(float maxTime) {
        EndTimeout();
        timeout.text = (maxTime < float.Epsilon) ? "" : string.Format(timeoutTextFormat, maxTime);
    }

    public void ShowTimeout(bool withSound, float maxTime, ICancellee cT, float? stayOnZero = null) {
        EndTimeout();
        if (maxTime < float.Epsilon) {
            timeout.text = "";
        } else {
            RunDroppableRIEnumerator(TimeoutCountdown(maxTime, withSound, stayOnZero ?? 3f, 
                new JointCancellee(cT, out timeoutCor)));
        }
    }

    private void EndTimeout() {
        timeoutCor?.Cancel();
        timeoutCor = null;
        timeout.text = "";
    }

    public SFXConfig[] countdownSounds = null!;

    private IEnumerator TimeoutCountdown(float maxTime, bool withSound, float stayOnZero, ICancellee cT) {
        if (cT.Cancelled) yield break;
        float currTime = maxTime;
        var currTimeIdent = -2;
        while (currTime > 0) {
            if (Mathf.RoundToInt(currTime * 10) != currTimeIdent) {
                timeout.text = StringBuffer.FormatPooled(timeoutTextFormat, currTime);
                currTimeIdent = Mathf.RoundToInt(currTime * 10);
            }
            yield return null;
            if (cT.Cancelled) yield break;
            var tryCross = Mathf.FloorToInt(currTime);
            currTime -= ETime.FRAME_TIME;
            if (withSound && currTime < tryCross) {
                if (0 < tryCross && tryCross <= countdownSounds.Length) {
                    ISFXService.SFXService.Request(countdownSounds[tryCross - 1]);
                }
            }
        }
        if (currTime < 1) {
            timeout.text = string.Format(timeoutTextFormat, 0);
        }
        while (stayOnZero > 0f) {
            yield return null;
            if (cT.Cancelled) yield break;
            stayOnZero -= ETime.FRAME_TIME;
        }
        timeout.text = "";
    }

    private IEnumerator FadeSpellname(float fit, Color from, Color to, ICancellee cT) {
        spellnameText.color = from;
        for (float t = 0; t < fit; t += ETime.FRAME_TIME) {
            yield return null;
            if (cT.Cancelled) yield break;
            spellnameText.color = Color.LerpUnclamped(from, to, t / fit);
        }
        spellnameText.color = to;
    }

    public void SetSpellname(string? title, (int success, int total)? rate = null) {
        if (rate.Try(out var r)) {
            cardSuccessContainer.SetActive(true);
            cardSuccessText.text = $"{r.success}";
            cardAttemptsText.text = $"{r.total}";
        } else {
            cardSuccessContainer.SetActive(false);
        }
        spellnameText.text = title ?? "";
        spellnameController?.Cancel();
        Run(FadeSpellname(spellnameFadeIn, spellColorTransparent, spellColor, 
            spellnameController = new Cancellable()), new() {
            Droppable = true,
            ExecType = CoroutineType.StepTryPrepend
        });
    }

    public Material bossColorizer = null!;

    public SpriteRenderer leftSidebar = null!;
    public SpriteRenderer rightSidebar = null!;
    public BossConfig.ProfileRender defaultProfile = null!;
    private readonly Stack<BossConfig.ProfileRender> stackedProfiles = new();

    public void CloseProfile() {
        var src = stackedProfiles.Pop();
        SetProfile(stackedProfiles.Peek(), src);
    }

    public void AddProfile(BossConfig.ProfileRender render) {
        SetProfile(render, profileTime < 0.1 ? null : stackedProfiles.Peek());
        stackedProfiles.Push(render);
    }

    public void SwitchProfile(BossConfig.ProfileRender render) {
        if (render == stackedProfiles.Peek()) return;
        SetProfile(render, stackedProfiles.Pop());
        stackedProfiles.Push(render);
    }

    private void SetProfile(BossConfig.ProfileRender target, BossConfig.ProfileRender? source) {
        profileTime = 0;
        if (source != null) {
            leftSidebarPB.SetTexture(PropConsts.fromTex, source.leftSidebar.Elvis(defaultProfile.leftSidebar));
            rightSidebarPB.SetTexture(PropConsts.fromTex, source.rightSidebar.Elvis(defaultProfile.rightSidebar));
        }
        leftSidebarPB.SetTexture(PropConsts.toTex, target.leftSidebar.Elvis(defaultProfile.leftSidebar));
        rightSidebarPB.SetTexture(PropConsts.toTex, target.rightSidebar.Elvis(defaultProfile.rightSidebar));
    }

    /// <summary>
    /// (int, Sprite) where int is the number of "boss lives" required to show the sprite
    /// </summary>
    public PrioritySprite[] bossHealthSprites = null!;

    public void SetBossColor(Color textColor, Color bossHPColor) {
        bossColorizer.SetMaterialOutline(textColor);
        foreach (var p in bossHealthSprites) {
            p.sprite.color = bossHPColor;
        }
    }
    [ContextMenu("set color green")]
    public void _setcolorgreen() => SetBossColor(Color.green, Color.green);
    [ContextMenu("set color red")]
    public void _setcolorred() => SetBossColor(Color.red, Color.red);

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

    public TextMeshPro phaseDescription = null!;

    public void ShowPhaseType(PhaseType? phase) {
        void Set(string? s) {
            if (s != null) phaseDescription.text = s;
        }
        Set(phase switch {
            PhaseType.Nonspell => "NON",
            PhaseType.Spell => "SPELL",
            PhaseType.Timeout => "SURVIVAL",
            PhaseType.FinalSpell => "FINAL",
            PhaseType.Stage => "STAGE",
            { } p when p.IsStageBoss() => "CHALLENGER\nAPPROACHING",
            PhaseType.Dialogue => null,
            null => null,
            _ => ""
        });
    }

    private static readonly Vector2 slideFrom = new(5, 0);

    public SpriteRenderer[] healthPoints = null!;
    public SpriteRenderer[] bombPoints = null!;
    public Sprite healthEmpty = null!;
    public Sprite[] healthItrs = null!;
    public Sprite[] bombItrs = null!;
    private const string pivMultFormat = "x<mspace=1.2>{0:00.00}</mspace>";
    private const string lifePointsFormat = "<mspace=1.5>{0}/{1}</mspace>";
    private const string grazeFormat = "<mspace=1.5>{0}</mspace>";
    private const string powerFormat = "<mspace=1.5>{0:F2}/{1:F2}</mspace>";
    private const string scoreFormat = "<mspace=1.7>{0}</mspace>";

    private static string ToMonospaceThousands(long val, float mspace = 1.7f) {
        string ms = $"<mspace={mspace}>";
        string msc = "</mspace>";
        StringBuilder sb = new();
        int pow = 0;
        for (; Math.Pow(10, pow + 3) < val; pow += 3) { }
        for (bool first = true; pow >= 0; pow -= 3, first = false) {
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

    private void InStayOutSpriteFade(TextMeshPro tmp, float timeIn, float timeStay, float timeOut, ICancellee cT,
        Action? done = null) {
        var m0 = tmp.color.WithA(0);
        var m1 = m0.WithA(1);
        new Tweener<Color>(m0, m1, timeIn, c => tmp.color = c, null, cT)
            .Then(new Tweener<float>(0, 0, timeStay, _ => { }, null, cT))
            .Then(new Tweener<Color>(m1, m0, timeOut, c => tmp.color = c, null, cT))
            .Run(this, new CoroutineOptions(true))
            .ContinueWithSync(done);
    }
    private void FadeMessage(string msg, ICancellee cT, float timeIn = 1f, float timeStay = 4f,
        float timeOut = 1f, Action? done = null) {
        message.text = msg;
        InStayOutSpriteFade(message, timeIn, timeStay, timeOut, cT, done);
    }

    private void FadeMessageCenter(string msg, ICancellee cT, out float totalTime,
        float timeIn = 0.2f, float timeStay = 0.8f, float timeOut = 0.3f) {
        centerMessage.text = msg;
        totalTime = timeIn + timeStay + timeOut;
        InStayOutSpriteFade(centerMessage, timeIn, timeStay, timeOut, cT);
    }

    private Cancellable? messageFadeToken;
    private readonly List<string> queuedMessages = new();

    private void _RunNextMessage() {
        if (queuedMessages.Count == 0)
            return;
        var msg = queuedMessages[0];
        FadeMessage(msg, messageFadeToken = new Cancellable(), done: () => {
            queuedMessages.RemoveAt(0);
            _RunNextMessage();
        });
    }
    private void _QueueMessage(string msg) {
        queuedMessages.Add(msg);
        if (queuedMessages.Count == 1)
            _RunNextMessage();
    }

    private Cancellable? cmessageFadeToken;

    private void _CMessage(string msg, out float totalTime) {
        cmessageFadeToken?.Cancel();
        FadeMessageCenter(msg, cmessageFadeToken = new Cancellable(), out totalTime);
    }

    public void MessageChallengeEnd(bool success, out float totalTime) => _CMessage(
        success ?
            "Challenge Pass!" :
            "Challenge Fail..."
        , out totalTime);

    private void Message(string msg) => _QueueMessage(msg);
    private void LifeExtendScore() => Message("Score Extend Acquired!");
    private void LifeExtendItems() => Message("Life Item Extend Acquired!");



    public void TrackBEH(BehaviorEntity beh, string title, ICancellee cT) {
        if (trackerPrefab != null)
            Instantiate(trackerPrefab).GetComponent<EdgeTracker>().Initialize(beh, title, cT);
    }

    public PiecewiseAppear stageAnnouncer = null!;
    public TextMeshPro stageDeannouncer = null!;

    private const float stageAnnounceStay = 2f;

    public void AnnounceStage(ICancellee cT, out float time) {
        time = 2 * (stageAnnouncer.moveTime + stageAnnouncer.spreadTime) + stageAnnounceStay;
        stageAnnouncer.Queue(new PiecewiseAppear.AppearRequest(PiecewiseAppear.AppearAction.APPEAR, 1f, () => 
            RUWaitingUtils.WaitThenCB(this, cT, stageAnnounceStay, false, () => 
                stageAnnouncer.Queue(new PiecewiseAppear.AppearRequest(PiecewiseAppear.AppearAction.DISAPPEAR, 0f, null)))));
    }

    private const float stageDAnnounceIn = 0.5f;
    private const float stageDAnnounceStay = 3f;
    private const float stageDAnnounceOut = 1f;

    public void DeannounceStage(ICancellee cT, out float time) {
        time = stageDAnnounceIn + stageDAnnounceOut + stageDAnnounceStay;
        InStayOutSpriteFade(stageDeannouncer, stageDAnnounceIn, stageAnnounceStay, stageDAnnounceOut, cT);
    }

    public TextMeshPro challengeHeader = null!;
    public TextMeshPro challengeText = null!;

    public void DisplayChallenge(PhaseChallengeRequest cr, SharedInstanceMetadata meta) {
        challengeHeader.text = cr.phase.Title(meta);
        challengeText.text = cr.Description;
    }
}
}
