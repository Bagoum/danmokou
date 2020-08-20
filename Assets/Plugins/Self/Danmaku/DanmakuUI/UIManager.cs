using System;
using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;
using System.Threading;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine.UIElements;
using static GameManagement;
using static Danmaku.Enums;

[Serializable]
public struct PrioritySprite {
    public int priority;
    public SpriteRenderer sprite;
}
public class UIManager : MonoBehaviour {
    private static UIManager main;
    private Camera cam;
    public XMLPauseMenu PauseManager;
    public static XMLPauseMenu PauseMenu => main.PauseManager;
    public XMLDeathMenu DeathManager;
    public TextMeshPro spellnameText;
    public TextMeshPro timeout;
    public TextMeshPro difficulty;
    public TextMeshPro score;
    public TextMeshPro maxScore;
    public TextMeshPro pivMult;
    public TextMeshPro lifePoints;
    public TextMeshPro graze;
    public TextMeshPro message;
    public TextMeshPro centerMessage;
    private const string deathCounterFormat = "死{0:D2}";
    private const string timeoutTextFormat = "<mspace=4.3>{0:F1}</mspace>";
    private const string fpsFormat = "FPS: <mspace=1.6>{0:F0}</mspace>";
    [CanBeNull] private static Coroutine timeoutCor;
    private static readonly int ValueID = Shader.PropertyToID("_Value");
    [CanBeNull] private Coroutine spellnameController;

    private Color spellColor;
    private Color spellColorTransparent;
    public float spellnameFadeIn = 1f;
    public float spellnameFadeOut = 0.5f;

    private float time = 0f;
    public SpriteRenderer PIVDecayBar;
    public SpriteRenderer BossHPBar;
    private MaterialPropertyBlock pivDecayPB;
    private MaterialPropertyBlock bossHPPB;
    private MaterialPropertyBlock profilePB;

    public GameObject trackerPrefab;

    private void Awake() {
        main = this;
        cam = GetComponent<Camera>();
        spellColor = spellnameText.color;
        spellColor.a = 1;
        spellColorTransparent = spellColor;
        spellColorTransparent.a = 0;
        PIVDecayBar.GetPropertyBlock(pivDecayPB = new MaterialPropertyBlock());
        BossHPBar.GetPropertyBlock(bossHPPB = new MaterialPropertyBlock());
        profileSr.GetPropertyBlock(profilePB = new MaterialPropertyBlock());
        timeout.text = "";
        bossName.text = "";
        bossTitle.text = "";
        spellnameText.text = "";
        message.text = centerMessage.text = "";
        challengeHeader.text = challengeText.text = "";
        UpdateTags();
        ShowBossLives(0);
        CloseProfile();
        SetBossHPLoader(null);
    }

    private void Start() {
        UpdatePB();
        _UpdatePlayerUI();
        GameStateManager.PauseAllowed = PauseManager.UI != null;
    }

    public static void UpdateTags() {
        main.difficulty.text = GameManagement.DifficultyString;
    }

    [CanBeNull] private static Enemy bossHP;

    public static void SetBossHPLoader([CanBeNull] Enemy boss) {
        bossHP = boss;
        main.BossHPBar.enabled = boss != null;
        if (boss != null) {
            main.bossHPPB.SetColor(PropConsts.fillColor2, bossHP.unfilledColor);
            main.bossHPPB.SetColor(PropConsts.unfillColor, bossHP.unfilledColor);
        }
    }
    private void UpdatePB() {
        pivDecayPB.SetFloat(PropConsts.time, time);
        pivDecayPB.SetFloat(PropConsts.fillRatio, (float)campaign.PIVDecay);
        pivDecayPB.SetFloat(PropConsts.innerFillRatio, Mathf.Clamp01((float)campaign.UIVisiblePIVDecayLenienceRatio));
        PIVDecayBar.SetPropertyBlock(pivDecayPB);
        bossHPPB.SetFloat(PropConsts.time, time);
        if (bossHP != null) {
            main.bossHPPB.SetColor(PropConsts.fillColor, bossHP.HPColor);
            bossHPPB.SetFloat(PropConsts.fillRatio, bossHP.DisplayHPRatio);
        }
        BossHPBar.SetPropertyBlock(bossHPPB);
    }

    public TextMeshPro fps;
    private const int fpsSmooth = 10;
    private int fpsUpdateCounter = fpsSmooth;
    private float accdT = 0f;

    private static bool campaignRequiresUpdate = false;
    private void Update() {
        time += ETime.dT;
        accdT += Time.deltaTime;
        if (--fpsUpdateCounter == 0) {
            fps.text = string.Format(fpsFormat, fpsSmooth / accdT);
            fpsUpdateCounter = fpsSmooth;
            accdT = 0;
        }
        UpdatePB();
        if (campaignRequiresUpdate) _UpdatePlayerUI();
        campaignRequiresUpdate = false;
    }

    public static void ShowStaticTimeout(float maxTime) {
        EndTimeout();
        main.timeout.text = (maxTime < float.Epsilon) ? "" : string.Format(timeoutTextFormat, maxTime);
    }
    public static void DoTimeout(bool withSound, float maxTime, 
        CancellationToken cT, float stayOnZero=3f) {
        EndTimeout();
        if (maxTime < float.Epsilon) {
            main.timeout.text = "";
        } else {
            timeoutCor = main.StartCoroutine(main.Timeout(maxTime, withSound, stayOnZero, cT));
        }
    }
    public static void EndTimeout() {
        if (timeoutCor != null) {
            main.StopCoroutine(timeoutCor);
            timeoutCor = null;
        }
        main.timeout.text = "";
    }

    public SFXConfig[] countdownSounds;
    private IEnumerator Timeout(float maxTime, bool withSound, float stayOnZero, CancellationToken cT) {
        float currTime = maxTime;
        var currTimeIdent = -2;
        while (currTime > 0) {
            if (Mathf.RoundToInt(currTime * 10) != currTimeIdent) {
                timeout.text = string.Format(timeoutTextFormat, currTime);
                currTimeIdent = Mathf.RoundToInt(currTime * 10);
            }
            yield return null;
            if (cT.IsCancellationRequested) { break; }
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

    private static IEnumerator FadeSpellname(float fit, Color from, Color to) {
        main.spellnameText.color = from;
        for (float t = 0; t < fit; t += ETime.dT) {
            yield return null;
            main.spellnameText.color = Color.LerpUnclamped(from, to, t / fit);
        }
        main.spellnameText.color  = to;
    }

    private void _SetSpellname(string title) {
        spellnameText.text = title;
        if (spellnameController != null) StopCoroutine(spellnameController);
        spellnameController = StartCoroutine(FadeSpellname(spellnameFadeIn, spellColorTransparent, spellColor));
    }
    public static void SetSpellname([CanBeNull] string title) {
        main._SetSpellname(title ?? "");
    }

    public TextMeshPro bossName;
    public TextMeshPro bossTitle;
    public Material bossColorizer;
    private static readonly int outlineColorProp = Shader.PropertyToID("_OutlineColor");
    private static readonly int underlayColorProp = Shader.PropertyToID("_UnderlayColor");
    public static void SetNameTitle(string name, string title) {
        main.bossName.text = name;
        main.bossTitle.text = title;
    }

    public SpriteRenderer profileSr;
    public BossConfig.ProfileRender defaultProfile;

    public static void CloseProfile() => SetProfile(main.defaultProfile);
    public static void SetProfile(BossConfig.ProfileRender render) {
        if (render.image == null) CloseProfile();
        else {
            main.profilePB.SetTexture(PropConsts.trueTex, render.image);
            main.profilePB.SetFloat(PropConsts.OffsetX, render.offsetX);
            main.profilePB.SetFloat(PropConsts.OffsetY, render.offsetY);
            main.profilePB.SetFloat(PropConsts.Zoom, render.zoom);
            main.profileSr.SetPropertyBlock(main.profilePB);
        }
    }

    /// <summary>
    /// (int, Sprite) where int is the number of "boss lives" required to show the sprite
    /// </summary>
    public PrioritySprite[] bossHealthSprites;
    public static void SetBossColor(Color textColor, Color bossHPColor) {
        main.bossColorizer.SetColor(outlineColorProp, textColor);
        main.bossColorizer.SetColor(underlayColorProp, textColor);
        foreach (var p in main.bossHealthSprites) {
            p.sprite.color = bossHPColor;
        }
    }

    public static void ShowBossLives(int bossLives) {
        foreach (var p in main.bossHealthSprites) {
            p.sprite.enabled = p.priority <= bossLives;
        }
    }
    public static void CloseBoss() {
        SetNameTitle("", "");
        SetSpellname(null);
        CloseProfile();
        ShowBossLives(0);
        SetBossHPLoader(null);
    }

    public TextMeshPro phaseDescription;
    public static void ShowPhaseType(PhaseType? phase) {
        void Set(string s) => main.phaseDescription.text = s;
        if (phase.HasValue) {
            var p = phase.Value;
            if (p == PhaseType.NONSPELL) Set("NON");
            else if (p == PhaseType.SPELL) Set("SPELL");
            else if (p == PhaseType.TIMEOUT) Set("TIMEOUT");
            else if (p == PhaseType.FINAL) Set("FINAL");
            else if (p == PhaseType.STAGE) Set("STAGE");
            else if (p.IsStageBoss()) Set("CHALLENGER\nAPPROACHING");
            else if (p == PhaseType.DIALOGUE) {}
            else Set("");
        }
    }

    private void HandleGameStateChange(GameState state) {
        if (state == GameState.RUN) {
            HidePauseMenu(true);
        } else if (state == GameState.PAUSE) {
            ShowPauseMenu();
        } else if (state == GameState.DEATH) {
            DeathManager.ShowMe();
            Message("YOU DIED");
        }
    }

    private DeletionMarker<Action<GameState>> gameStateListener;
    private void OnEnable() {
        gameStateListener = Core.Events.GameStateHasChanged.Listen(HandleGameStateChange);
    }

    private void OnDisable() {
        gameStateListener.MarkForDeletion();
        HidePauseMenu(false);
    }

    public SpriteRenderer[] healthPoints;
    public Sprite healthEmpty;
    public Sprite[] healthItrs;
    private const string pivMultFormat = "x<mspace=1.7>{0:F2}</mspace>";
    private const string lifePointsFormat = "<mspace=1.5>{0}/{1}</mspace>";
    private const string grazeFormat = "<mspace=1.5>{0}</mspace>";

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
        score.text = ToMonospaceThousands(campaign.UIVisibleScore); 
        maxScore.text = ToMonospaceThousands(campaign.maxScore);
        pivMult.text = string.Format(pivMultFormat, campaign.PIV);
        lifePoints.text = string.Format(lifePointsFormat, campaign.LifeItems, campaign.NextLifeItems);
        graze.text = string.Format(grazeFormat, campaign.Graze);
        for (int ii = 0; ii < healthPoints.Length; ++ii) healthPoints[ii].sprite = healthEmpty;
        for (int hi = 0; hi < healthItrs.Length; ++hi) {
            for (int ii = 0; ii + hi * healthPoints.Length < campaign.Lives && ii < healthPoints.Length; ++ii) {
                healthPoints[ii].sprite = healthItrs[hi];
            }
        }
        //int extra = campaign.Lives - healthPoints.Length;
        //extraHP.text = extra > 0 ? string.Format(extraHPFormat, extra) : "";
    }

    public static void UpdatePlayerUI() => campaignRequiresUpdate = true;

    private IEnumerator FadeMessage(string msg, CancellationToken cT, float timeIn = 1f, float timeStay = 4f,
        float timeOut = 1f) {
        message.text = msg;
        return FadeSprite(message.color, c => message.color = c, timeIn, timeStay, timeOut, cT);
    }
    private IEnumerator FadeMessageCenter(string msg, CancellationToken cT, out float totalTime, 
        float timeIn = 0.5f, float timeStay = 1f, float timeOut = 0.5f) {
        centerMessage.text = msg;
        totalTime = timeIn + timeStay + timeOut;
        return FadeSprite(centerMessage.color, c => centerMessage.color = c, timeIn, timeStay, timeOut, cT);
    }

    private static IEnumerator FadeSprite(Color c, Action<Color> apply, float timeIn, float timeStay,
        float timeOut, CancellationToken cT) {
        for (float t = 0; t < timeIn; t += ETime.dT) {
            c.a = t / timeIn;
            apply(c);
            if (cT.IsCancellationRequested) break;
            yield return null;
        }
        c.a = 1;
        apply(c);
        for (float t = 0; t < timeStay; t += ETime.dT) {
            if (cT.IsCancellationRequested) break;
            yield return null;
        }
        for (float t = 0; t < timeOut; t += ETime.dT) {
            c.a = 1 - t / timeOut;
            apply(c);
            if (cT.IsCancellationRequested) break;
            yield return null;
        }
        c.a = 0;
        apply(c);
    }

    [CanBeNull] private static CancellationTokenSource messageFadeToken;

    private void _Message(string msg) {
        //TODO this isn't everdisposed is it? im really leaning towards making a custom CTS to avoid this kinda stuff...
        messageFadeToken?.Cancel();
        StartCoroutine(FadeMessage(msg, (messageFadeToken = new CancellationTokenSource()).Token));
    }
    [CanBeNull] private static CancellationTokenSource cmessageFadeToken;

    private void _CMessage(string msg, out float totalTime) {
        //TODO this isn't everdisposed is it? im really leaning towards making a custom CTS to avoid this kinda stuff...
        cmessageFadeToken?.Cancel();
        StartCoroutine(FadeMessageCenter(msg, (cmessageFadeToken = new CancellationTokenSource()).Token, out totalTime));
    }

    public static void MessageChallengeEnd(bool success, out float totalTime) => main._CMessage(
        success ?
            "Challenge Pass!" :
            "Challenge Fail..."
        , out totalTime);

    private static void Message(string msg) => main._Message(msg);
    public static void LifeExtendScore() => Message("Score Extend Acquired!");
    public static void LifeExtendItems() => Message("Life Item Extend Acquired!");
    public static void CardCapture(PhaseCompletion pc) {
        if (pc.Captured.HasValue) {
            Message(pc.Captured.Value
                ? "Card Captured!"
                : "Card Capture Failed..."
                );
        }
    }


    [ContextMenu("Pause")]
    private void ShowPauseMenu() => PauseManager.ShowOptions();


    [ContextMenu("Unpause")]
    private void _hidepause() => HidePauseMenu(false);

    private void HidePauseMenu(bool doSave) => PauseManager.HideOptions(doSave);


    public static BottomTracker TrackBEH(BehaviorEntity beh, string title, CancellationToken cT) => 
        Instantiate(main.trackerPrefab).GetComponent<BottomTracker>().Initialize(beh, title, cT);

    public SpriteRenderer stageAnnouncer;
    public TextMeshPro stageDeannouncer;

    private const float stageAnnounceIn = 1f;
    private const float stageAnnounceStay = 3f;
    private const float stageAnnounceOut = 1f;
    public static void AnnounceStage(CancellationToken cT, out float time) {
        time = stageAnnounceIn + stageAnnounceOut + stageAnnounceStay;
        main.StartCoroutine(FadeSprite(Color.white, c => main.stageAnnouncer.color = c, stageAnnounceIn, stageAnnounceStay,
            stageAnnounceOut, cT));
    }
    private const float stageDAnnounceIn = 0.5f;
    private const float stageDAnnounceStay = 3f;
    private const float stageDAnnounceOut = 1f;
    public static void DeannounceStage(CancellationToken cT, out float time) {
        time = stageDAnnounceIn + stageDAnnounceOut + stageDAnnounceStay;
        main.StartCoroutine(FadeSprite(Color.white, c => main.stageDeannouncer.color = c, stageDAnnounceIn, stageDAnnounceStay,
            stageDAnnounceOut, cT));
    }

    public TextMeshPro challengeHeader;
    public TextMeshPro challengeText;

    public static void RequestChallengeDisplay(ChallengeRequest cr) {
        main.challengeHeader.text = cr.phase.Title;
        main.challengeText.text = cr.Description;
    }
}
